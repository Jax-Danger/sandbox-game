using Prowl.Runtime;
using Prowl.Vector;
using Prowl.Runtime.Resources;

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
        // Engine 1.0: positive pitch tilts Forward down (v1.0 was the opposite), so the v1.0 angles are negated.
        float pitch = -(elev > 0.08f ? -MathF.Max(6f, elev * 75f) : MathF.Min(30f, (0.08f - elev) * 60f) - 4.8f);
        float yaw = 30f + t * 360f;
        g.Sun.Transform.LocalEulerAngles = new Float3(pitch, yaw, 0f);
        g.Sun.Intensity = 0.45f + (1.5f * SunScale - 0.45f) * day; // night (moon) level unchanged; // brighter moon: the ACES toe crushed 0.15 to black
        g.Sun.Color = Lerp(new Color(0.55f, 0.62f, 0.95f, 1f), Lerp(new Color(1f, 0.97f, 0.92f, 1f), new Color(1f, 0.62f, 0.35f, 1f), warm), day);

        Prowl.Runtime.Resources.Scene w = g.World;
        Float4 skyDay = new(0.55f, 0.65f, 0.80f, 1f), skyNight = new(0.16f, 0.20f, 0.34f, 1f), skyWarm = new(0.85f, 0.55f, 0.45f, 1f);
        Float4 sky = L4(L4(skyNight, skyDay, day), skyWarm, warm * 0.5f);
        w.Environment.Ambient.SkyColor = C(sky);
        Color fog = Lerp(new Color(0.06f, 0.08f, 0.15f, 1f), Lerp(new Color(0.72f, 0.80f, 0.88f, 1f), new Color(0.93f, 0.62f, 0.44f, 1f), warm * 0.75f), day);
        if (Environment.GetEnvironmentVariable("SANDBOX_SOLIDSKY") != "1")
        {
            g.Player.Camera.Background = CameraBackground.Skybox;
            g.Player.Camera.BackgroundColor = fog;
            if (Sky(g) is { } m)
            {
                Float3 toSun = -g.Sun.Transform.Forward;
                Color zen = Lerp(new Color(0.008f, 0.012f, 0.035f, 1f), Lerp(new Color(0.13f, 0.34f, 0.86f, 1f), new Color(0.13f, 0.17f, 0.42f, 1f), warm), day);
                m.SetColor("_Zenith", zen);
                m.SetColor("_Horizon", fog);
                m.SetColor("_Glow", new Color(1f, 0.48f, 0.22f, 1f));
                m.SetVector("_SunDir", new Float4(toSun.X, toSun.Y, toSun.Z, 0f));
                m.SetVector("_MoonDir", new Float4(-toSun.X * 0.6f + 0.3f, MathF.Abs(toSun.Y) * 0.8f + 0.35f, -toSun.Z * 0.6f + 0.4f, 0f));
                m.SetFloat("_Night", Math.Clamp(1f - day * 2.2f, 0f, 1f));
                m.SetFloat("_GlowAmt", warm);
            }
        }
        w.Environment.Ambient.GroundColor = C(L4(new Float4(0.12f, 0.12f, 0.16f, 1f), new Float4(0.30f, 0.28f, 0.26f, 1f), day));
        w.Environment.Ambient.Intensity = 0.8f + (1.05f * AmbientScale - 0.8f) * day; // night level unchanged; // ACES deepens shadow sides; lift sky fill a little
        // Town lamps: glass glows and a warm point light fades in as the sun goes down.
        float lit = Math.Clamp(1f - day * 1.6f, 0f, 1f);
        foreach (var (glass, light) in Town.NightLamps)
        {
            try { glass.SetFloat("_EmissionIntensity", light is null ? 1.05f * lit : 0.2f + 2.8f * lit); } catch { }
            if (light is not null) { light.Intensity = 3.5f * lit; light.Enabled = lit > 0.01f; }
        }
        w.Environment.Fog.Color = fog;
        g.HorizonColor(fog);
    }

    /// <summary>Engine 1.0 retune (once the sun pointed down again): v1.0 intensities washed the floor out to near white
    /// (floor luma 227 vs 172 in the v1.0 reference shot); these scale the full-day values (night levels unchanged) to bring it to ~200 with crisp sun shadows.</summary>
    public static float SunScale = 0.55f, AmbientScale = 0.7f;
    private static Material? _sky; private static bool _skyTried;
    /// <summary>Editor play sessions share statics; forget the previous session's sky material.</summary>
    internal static void ResetStatics() { _sky = null; _skyTried = false; }
    /// <summary>Our own sky shader (gradient matched to fog, sun glow, stars, moon) replaces the engine's procedural sky.</summary>
    private static Material? Sky(SandboxGame g)
    {
        if (_skyTried || Application.IsHeadless) return _sky;
        _skyTried = true;
        try
        {
            string path = Path.Combine(AssetRegistry.Root, "shaders", "SandboxSky.shader");
            _sky = new Material(Shader.LoadFromFile(path));
            g.World.Environment.SkyMaterial = _sky;
        }
        catch (Exception e) { Console.WriteLine($"[sky] custom sky failed: {e.Message}"); _sky = null; }
        return _sky;
    }

    private static Color Lerp(Color a, Color b, float t) => new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, 1f);
    public static Color C(Float4 v) => new(v.X, v.Y, v.Z, v.W);
    private static Float4 L4(Float4 a, Float4 b, float t) => a + (b - a) * t;
}
