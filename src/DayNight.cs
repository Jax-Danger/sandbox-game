using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>Day/night cycle and sky presets: drives the sun angle/colour, hemisphere ambient and fog together.</summary>
public sealed class DayNightCycle
{
    public bool Enabled { get; set; }
    /// <summary>0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset.</summary>
    public float TimeOfDay { get; set; } = 0.42f;
    public float DayLengthSeconds { get; set; } = 600f;

    public static readonly (string Name, float Time)[] Presets = [("Morning", 0.3f), ("Noon", 0.5f), ("Golden hour", 0.71f), ("Dusk", 0.755f), ("Night", 0.95f)];

    public void Tick(SandboxGame g, float dt)
    {
        if (Enabled) TimeOfDay = (TimeOfDay + dt / DayLengthSeconds) % 1f;
        Apply(g);
    }

    public void Apply(SandboxGame g)
    {
        float t = TimeOfDay;
        float elev = MathF.Sin((t - 0.25f) * MathF.Tau);          // -1 midnight … 1 noon
        float day = Math.Clamp(elev * 2.6f + 0.55f, 0f, 1f);        // 0 night, 1 day (soft dusk)
        float warm = Math.Clamp(1f - MathF.Abs(elev) * 3.2f, 0f, 1f) * (elev > -0.15f ? 1f : 0f); // sunrise/sunset tint

        // Above the horizon the sun points down; below it, it dips under so the procedural sky darkens for night.
        float pitch = elev > 0.08f ? -MathF.Max(6f, elev * 75f) : MathF.Min(30f, (0.08f - elev) * 60f) - 4.8f;
        float yaw = 30f + t * 360f;
        g.Sun.Transform.LocalEulerAngles = new Float3(pitch, yaw, 0f);
        g.Sun.Intensity = 0.15f + 1.35f * day;
        g.Sun.Color = Lerp(new Color(0.55f, 0.62f, 0.95f, 1f), Lerp(new Color(1f, 0.97f, 0.92f, 1f), new Color(1f, 0.62f, 0.35f, 1f), warm), day);

        Prowl.Runtime.Resources.Scene w = g.World;
        Float4 skyDay = new(0.55f, 0.65f, 0.80f, 1f), skyNight = new(0.06f, 0.08f, 0.16f, 1f), skyWarm = new(0.85f, 0.55f, 0.45f, 1f);
        Float4 sky = L4(L4(skyNight, skyDay, day), skyWarm, warm * 0.5f);
        w.Ambient.SkyColor = sky;
        w.Ambient.GroundColor = L4(new Float4(0.05f, 0.05f, 0.07f, 1f), new Float4(0.30f, 0.28f, 0.26f, 1f), day);
        w.Ambient.Strength = 0.45f + 0.45f * day;
        w.Fog.Color = Lerp(new Color(0.07f, 0.09f, 0.15f, 1f), Lerp(new Color(0.72f, 0.80f, 0.88f, 1f), new Color(0.92f, 0.70f, 0.55f, 1f), warm * 0.6f), day);
    }

    private static Color Lerp(Color a, Color b, float t) => new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, 1f);
    private static Float4 L4(Float4 a, Float4 b, float t) => a + (b - a) * t;
}
