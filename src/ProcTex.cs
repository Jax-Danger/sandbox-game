using Prowl.Runtime;
using Prowl.Runtime.Resources;

namespace Sandbox;

/// <summary>Small procedural textures (no files needed): solids, surface maps, wood, concrete, dev floor.</summary>
public static class ProcTex
{
    private static readonly Dictionary<string, Texture2D> Cache = [];

    public static Texture2D Make(string key, int w, int h, Func<int, int, (byte r, byte g, byte b, byte a)> px)
    {
        if (Cache.TryGetValue(key, out Texture2D? t)) return t;
        var data = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var (r, g, b, a) = px(x, y);
                int i = (y * w + x) * 4;
                data[i] = r; data[i + 1] = g; data[i + 2] = b; data[i + 3] = a;
            }
        // Same upload path as Prowl's image importer (Magick → Texture2D.FromImage, 16-bit RGBA, flipped rows).
        var settings = new ImageMagick.MagickReadSettings { Width = (uint)w, Height = (uint)h, Format = ImageMagick.MagickFormat.Rgba, Depth = 8 };
        using (var img = new ImageMagick.MagickImage(data, settings))
            t = Texture2D.FromImage(img, generateMipmaps: true);
        try { t.SetWrapModes(TextureWrap.Repeat, TextureWrap.Repeat); } catch { }
        return Cache[key] = t;
    }

    /// <summary>Texture generators the loading screen runs one per frame batch.</summary>
    public static List<Action> WarmList() => [() => DevFloor(), () => Grass(), () => Concrete(), () => Wood(), () => Panel()];

    public static Texture2D Solid(byte r, byte g, byte b, byte a = 255) => Make($"solid{r},{g},{b},{a}", 2, 2, (_, _) => (r, g, b, a));

    /// <summary>Surface map: R = occlusion (the Standard shader uses ao = 1 - R, so 0 = fully lit), G = roughness, B = metallic.</summary>
    public static Texture2D Surface(float roughness, float metallic, float occlusion = 0f) =>
        Solid((byte)(occlusion * 255), (byte)(roughness * 255), (byte)(metallic * 255));

    private static float Hash(int x, int y, int seed = 0)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 2147483647;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }

    private static float Noise(float x, float y, int seed)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    private static float Fbm(float x, float y, int seed)
    {
        float v = 0, amp = 0.5f;
        for (int i = 0; i < 4; i++) { v += Noise(x, y, seed + i) * amp; x *= 2; y *= 2; amp *= 0.5f; }
        return v;
    }

    private static byte B(float v) => (byte)Math.Clamp(v * 255f, 0f, 255f);

    /// <summary>Light dev-texture floor: soft concrete noise with a subtle 1m grid (tile 4m per texture).</summary>
    public static Texture2D DevFloor() => Make("devfloor", 256, 256, (x, y) =>
    {
        float n = 0.46f + 0.05f * Fbm(x / 24f, y / 24f, 3);
        bool line = x % 64 < 2 || y % 64 < 2;
        bool major = x < 3 || y < 3;
        float v = n * (major ? 0.55f : line ? 0.72f : 1f);
        return (B(v), B(v), B(v * 1.01f), 255);
    });

    /// <summary>Lake surface: deep teal with soft wind-ripple bands and the odd bright glint streak (tiles seamlessly).</summary>
    public static Texture2D Water() => Make("water", 128, 128, (x, y) =>
    {
        float rip = Fbm(x / 6f, y / 18f, 21), broad = Fbm(x / 30f, y / 30f, 22);
        float v = 0.82f + 0.22f * rip + 0.12f * broad;
        float glint = MathF.Max(0f, rip - 0.72f) * 3.5f;
        return (B(v * 0.135f + glint * 0.35f), B(v * 0.33f + glint * 0.4f), B(v * 0.44f + glint * 0.42f), 255);
    });

    public static Texture2D Grass() => Make("grass", 128, 128, (x, y) =>
    {
        float v = 0.8f + 0.25f * Fbm(x / 9f, y / 9f, 11) + (Hash(x, y, 4) - 0.5f) * 0.12f;
        float dry = Math.Clamp(Fbm(x / 23f, y / 23f, 5) * 1.6f - 0.3f, 0f, 1f);
        return (B(v * (0.30f + 0.12f * dry)), B(v * (0.46f + 0.04f * dry)), B(v * (0.20f + 0.04f * dry)), 255);
    });

    public static Texture2D Concrete() => Make("concrete", 128, 128, (x, y) =>
    {
        float v = 0.52f + 0.12f * Fbm(x / 10f, y / 10f, 7) - (Hash(x, y, 9) > 0.985f ? 0.15f : 0f);
        return (B(v), B(v), B(v), 255);
    });

    public static Texture2D Wood() => Make("wood", 128, 128, (x, y) =>
    {
        float grain = MathF.Sin((y + Fbm(x / 30f, y / 6f, 5) * 18f) * 0.55f) * 0.5f + 0.5f;
        float v = 0.78f + 0.22f * grain;
        return (B(v), B(v * 0.93f), B(v * 0.86f), 255);
    });

    /// <summary>Gunstock walnut: fine, long, slightly wavy grain with darker pores and figure, tileable in both axes.</summary>
    public static Texture2D WalnutGrain() => Make("walnut", 256, 256, (x, y) =>
    {
        float u = x / 256f, w = y / 256f;
        float wave = MathF.Sin(w * MathF.Tau * 2f + MathF.Sin(u * MathF.Tau * 3f) * 0.8f) * 0.04f;
        float g = MathF.Sin((u + wave) * MathF.Tau * 22f + MathF.Sin(w * MathF.Tau) * 2f) * 0.5f + 0.5f;
        float fine = MathF.Sin((u + wave * 1.7f) * MathF.Tau * 71f) * 0.5f + 0.5f;
        float pore = ((x * 73856093) ^ (y * 19349663)) % 97 == 0 ? 0.75f : 1f;
        float v = (0.72f + 0.2f * MathF.Pow(g, 3f) + 0.08f * fine) * pore;
        return (B(v), B(v * 0.9f), B(v * 0.82f), 255);
    });

    /// <summary>Glove fabric for swept VM parts (u = around, v = along at 8 per metre): fine knit, plus stitched side
    /// seams at u = 0.25 / 0.75 so fingers and palms read as sewn panels rather than clay.</summary>
    public static Texture2D GloveKnit() => Make("gloveknit", 128, 128, (x, y) =>
    {
        float knit = ((x + (y / 2) % 2) % 2 == 0 ? 1f : 0.93f) * (y % 3 == 0 ? 0.95f : 1f);
        float n = 0.96f + 0.04f * Fbm(x / 9f, y / 9f, 3);
        float v = knit * n;
        foreach (int sx in new[] { 32, 96 })
        {
            int dx = Math.Abs(x - sx);
            if (dx <= 1) v *= 0.62f;                                          // seam groove
            else if (dx == 3 && (y / 3) % 2 == 0) v *= 1.22f;                // running stitch beside it
        }
        return (B(v), B(v), B(v), 255);
    });

    /// <summary>Crate/prop panel: lighter border frame with noise, so props read well without the engine grid.</summary>
    public static Texture2D Panel() => Make("panel", 128, 128, (x, y) =>
    {
        int e = Math.Min(Math.Min(x, 127 - x), Math.Min(y, 127 - y));
        float frame = e < 6 ? 1.1f : e < 9 ? 0.82f : 1f;
        float v = (0.9f + 0.08f * Fbm(x / 16f, y / 16f, 11)) * frame;
        return (B(v), B(v), B(v), 255);
    });
}

/// <summary>Physical "material" presets for props: visual surface + friction/bounciness.</summary>
public sealed record SurfaceMaterial(string Name, float Roughness, float Metallic, float Friction, float Restitution, Func<Texture2D> Albedo)
{
    public static readonly SurfaceMaterial[] All =
    [
        new("Plastic", 0.55f, 0f, 0.6f, 0.15f, ProcTex.Panel),
        new("Wood", 0.8f, 0f, 0.7f, 0.1f, ProcTex.Wood),
        new("Metal", 0.3f, 1f, 0.45f, 0.05f, ProcTex.Panel),
        new("Rubber", 0.95f, 0f, 1f, 0.75f, () => ProcTex.Solid(255, 255, 255)),
        new("Ice", 0.05f, 0.1f, 0.02f, 0.05f, () => ProcTex.Solid(235, 245, 255)),
        new("Concrete", 0.9f, 0f, 0.8f, 0.05f, ProcTex.Concrete),
    ];

    public static SurfaceMaterial Get(string name) => All.FirstOrDefault(m => m.Name == name) ?? All[0];

    public void ApplyVisual(Material m)
    {
        if (Application.IsHeadless || Environment.GetEnvironmentVariable("SANDBOX_PROCTEX") == "0") return; // opt out with SANDBOX_PROCTEX=0
        m.SetTexture("_MainTex", Albedo());
        m.SetTexture("_SurfaceTex", ProcTex.Surface(Roughness, Metallic));
    }
}
