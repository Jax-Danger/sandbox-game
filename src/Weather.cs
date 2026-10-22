using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

public enum WeatherKind { Clear, Overcast, Fog, Rain, Storm }

/// <summary>
/// Weather presets layered on top of the day/night lighting: darker sun, greyer/denser fog, rain streaks that
/// follow the camera, and lightning flashes in storms. Blends smoothly between presets; saved per world.
/// </summary>
public sealed class WeatherSystem
{
    public WeatherKind Kind { get; private set; } = WeatherKind.Clear;
    public static readonly string[] Names = Enum.GetNames<WeatherKind>();
    // Per-preset targets: sun multiplier, fog start, fog end, greyness, rain amount.
    private static (float Sun, float Start, float End, float Grey, float Rain) Target(WeatherKind k) => k switch
    {
        WeatherKind.Overcast => (0.45f, 30f, 170f, 0.55f, 0f),
        WeatherKind.Fog => (0.35f, 0f, 32f, 0.65f, 0f),
        WeatherKind.Rain => (0.30f, 12f, 110f, 0.75f, 0.7f),
        WeatherKind.Storm => (0.18f, 6f, 80f, 0.9f, 1f),
        _ => (1f, 100f, 284f, 0f, 0f), // clear: light haze only (hills stay readable; the horizon skirt at 290 m is still past fog end)
    };
    private float _sun = 1f, _start = 100f, _end = 284f, _grey, _rain, _flash, _nextBolt = 6f;
    public float RainAmount => _rain;
    public int ActiveDrops { get; private set; }
    public int Bolts { get; private set; }
    private readonly List<(GameObject Go, MeshRenderer R)> _drops = [];
    private readonly Random _rng = new(7);
    private Material? _dropMat;
    public const int MaxDrops = 700;
    public bool Settled => MathF.Abs(_sun - Target(Kind).Sun) < 0.01f && MathF.Abs(_rain - Target(Kind).Rain) < 0.01f;

    public void Set(WeatherKind k, SandboxGame g, bool instant = false)
    {
        Kind = k;
        if (g.Meta is { } m) m.Modifiers.Weather = k.ToString();
        if (instant) { var t = Target(k); _sun = t.Sun; _start = t.Start; _end = t.End; _grey = t.Grey; _rain = t.Rain; }
    }

    public static WeatherKind Parse(string? s) => Enum.TryParse(s, true, out WeatherKind k) ? k : WeatherKind.Clear;

    public void Tick(SandboxGame g, float dt)
    {
        var t = Target(Kind);
        float k = 1f - MathF.Exp(-dt * 0.8f); // ~3 s blend
        _sun += (t.Sun - _sun) * k; _start += (t.Start - _start) * k; _end += (t.End - _end) * k; _grey += (t.Grey - _grey) * k; _rain += (t.Rain - _rain) * k;
        if (Kind == WeatherKind.Storm)
        {
            _nextBolt -= dt;
            if (_nextBolt <= 0f) { _nextBolt = 5f + _rng.NextSingle() * 9f; _flash = 1f; Bolts++; g.Sounds.Play("sfx_explosion", 0.25f); }
        }
        _flash = MathF.Max(0f, _flash - dt * 3.5f);
        bool clear = Kind == WeatherKind.Clear && _grey < 0.005f && _rain < 0.005f && _flash <= 0f;
        if (!clear || _wasActive)
        {
            g.DayNight.Apply(g); // base lighting, then layer weather on top (no compounding)
            if (!clear) ApplyLighting(g);
        }
        _wasActive = !clear;
        TickRain(g, dt);
    }
    private bool _wasActive;

    private void ApplyLighting(SandboxGame g)
    {
        Scene w = g.World;
        float flash = _flash * _flash;
        g.Sun.Intensity = g.Sun.Intensity * _sun + flash * 2.5f;
        Color f = w.Environment.Fog.Color, grey = new(0.52f, 0.55f, 0.58f, 1f);
        float lum = (f.R + f.G + f.B) / 3f; // keep night dark
        grey = new Color(grey.R * MathF.Min(1f, lum * 1.4f), grey.G * MathF.Min(1f, lum * 1.4f), grey.B * MathF.Min(1f, lum * 1.4f), 1f);
        w.Environment.Fog.Color = new Color(f.R + (grey.R - f.R) * _grey + flash * 0.4f, f.G + (grey.G - f.G) * _grey + flash * 0.4f, f.B + (grey.B - f.B) * _grey + flash * 0.45f, 1f);
        w.Environment.Fog.Start = _start; w.Environment.Fog.End = _end;
        w.Environment.Ambient.Intensity *= 1f - _grey * 0.25f;
        // The procedural sky can't be greyed, so under heavy cloud clear to the fog colour (the horizon then matches).
        if (_grey > 0.3f) { g.Player.Camera.Background = CameraBackground.Color; g.Player.Camera.BackgroundColor = w.Environment.Fog.Color; }
        w.Environment.Ambient.SkyColor = DayNightCycle.C(new Float4(w.Environment.Ambient.SkyColor.R, w.Environment.Ambient.SkyColor.G, w.Environment.Ambient.SkyColor.B, 1f) * (1f - _grey * 0.35f) + new Float4(flash * 0.5f, flash * 0.5f, flash * 0.6f, 0f));
    }

    private void TickRain(SandboxGame g, float dt)
    {
        int want = Application.IsHeadless ? 0 : (int)(MaxDrops * _rain);
        while (_drops.Count < want)
        {
            var go = new GameObject("raindrop");
            var r = go.AddComponent<MeshRenderer>();
            r.Mesh = _dropMesh ??= Mesh.CreateCube(new Float3(0.016f, 0.7f, 0.016f));
            if (_dropMat is null) { _dropMat = Mats.New(g.Shader); _dropMat.SetColor("_MainColor", new Color(0.75f, 0.82f, 0.95f, 1f)); }
            r.Material = _dropMat;
            go.Transform.Position = RandomAround(g.Player.EyePosition, true);
            g.World.Add(go);
            _drops.Add((go, r));
        }
        _drops.RemoveAll(d => !d.Go.IsValid() || d.Go.Scene == null); // map rebuilds clear the scene
        ActiveDrops = 0;
        Float3 eye = g.Player.EyePosition;
        float speed = 16f + 6f * _rain;
        for (int i = 0; i < _drops.Count; i++)
        {
            var (go, r) = _drops[i];
            bool on = i < want;
            if (r.Enabled != on) r.Enabled = on;
            if (!on) continue;
            ActiveDrops++;
            Float3 p = go.Transform.Position + new Float3(1.5f, -speed, 0.6f) * dt;
            Float3 d = p - eye;
            if (p.Y < eye.Y - 6f || MathF.Abs(d.X) > 14f || MathF.Abs(d.Z) > 14f) p = RandomAround(eye, false);
            go.Transform.Position = p;
        }
    }
    private Mesh? _dropMesh;

    private Float3 RandomAround(Float3 eye, bool anyHeight) =>
        eye + new Float3(_rng.NextSingle() * 28f - 14f, anyHeight ? _rng.NextSingle() * 16f - 6f : 8f + _rng.NextSingle() * 4f, _rng.NextSingle() * 28f - 14f);

    /// <summary>Called when the scene is rebuilt (map change): pooled drops belonged to the old scene.</summary>
    public void Reset()
    {
        foreach (var (go, _) in _drops) if (go.IsValid() && go.Scene != null) go.Scene.Remove(go);
        _drops.Clear();
    }
}
