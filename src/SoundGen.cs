namespace Sandbox;

/// <summary>
/// Procedural placeholder sounds. Each id is synthesised (noise bursts, tones, envelopes) into a 16-bit mono WAV
/// in a cache folder the first time it's needed. A real file at assets/sounds/&lt;id&gt;.wav always wins.
/// </summary>
public static class SoundGen
{
    public const int Rate = 22050;
    private const int Version = 1;
    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProwlSandbox", $"sounds-gen-v{Version}");

    public static readonly string[] Ids =
    [
        "sfx_spawn", "sfx_weapon_switch", "sfx_ui_click", "sfx_ui_hover", "sfx_pickup", "sfx_throw", "sfx_impact_soft", "sfx_impact",
        "sfx_explosion", "sfx_pistol", "sfx_smg", "sfx_shotgun", "sfx_rpg", "sfx_dry", "sfx_reload", "sfx_swing", "sfx_door",
        "sfx_engine", "sfx_button", "sfx_physgun_hum", "sfx_footstep", "sfx_tool",
    ];

    /// <summary>Returns the generated file for <paramref name="id"/>, creating it if needed; null if unknown or on failure.</summary>
    public static string? PathFor(string id)
    {
        if (!Ids.Contains(id)) return null;
        string path = Path.Combine(Dir, $"{id}.wav");
        if (File.Exists(path)) return path;
        try
        {
            Directory.CreateDirectory(Dir);
            float[] s = Synth(id);
            WriteWav(path, s);
            return path;
        }
        catch (Exception e) { Console.WriteLine($"[soundgen] {id}: {e.Message}"); return null; }
    }

    public static void WriteWav(string path, float[] samples)
    {
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        int bytes = samples.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + bytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(bytes);
        foreach (float f in samples) w.Write((short)(Math.Clamp(f, -1f, 1f) * 32000f));
    }

    private static float[] Buf(float seconds) => new float[(int)(seconds * Rate)];

    private static float[] Synth(string id)
    {
        var rng = new Random(id.GetHashCode() & 0x7fffffff);
        float N() => rng.NextSingle() * 2f - 1f;
        float[] b;
        switch (id)
        {
            case "sfx_pistol": case "sfx_smg": case "sfx_shotgun":
            {
                float len = id == "sfx_shotgun" ? 0.6f : id == "sfx_smg" ? 0.18f : 0.3f;
                float decay = id == "sfx_shotgun" ? 9f : id == "sfx_smg" ? 30f : 18f;
                b = Buf(len);
                float lp = 0f;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    lp += (N() - lp) * (id == "sfx_shotgun" ? 0.25f : 0.45f);
                    float thump = MathF.Sin(2f * MathF.PI * (90f - 60f * t) * t) * MathF.Exp(-t * 25f);
                    b[i] = (lp * 1.4f * MathF.Exp(-t * decay) + thump * 0.8f) * (t < 0.002f ? t / 0.002f : 1f);
                }
                break;
            }
            case "sfx_rpg":
            {
                b = Buf(0.9f); float lp = 0f;
                for (int i = 0; i < b.Length; i++) { float t = i / (float)Rate; lp += (N() - lp) * 0.08f; b[i] = lp * 2.5f * MathF.Exp(-t * 3f) * MathF.Min(1f, t * 40f); }
                break;
            }
            case "sfx_explosion":
            {
                b = Buf(1.8f); float lp = 0f, lp2 = 0f;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    lp += (N() - lp) * 0.05f; lp2 += (lp - lp2) * 0.3f;
                    float boom = MathF.Sin(2f * MathF.PI * (55f - 25f * t) * t) * MathF.Exp(-t * 4f);
                    b[i] = (lp2 * 4f * MathF.Exp(-t * 2.2f) + boom) * MathF.Min(1f, t * 200f);
                }
                break;
            }
            case "sfx_impact": case "sfx_impact_soft": case "sfx_footstep":
            {
                float len = id == "sfx_footstep" ? 0.12f : 0.2f;
                b = Buf(len); float lp = 0f;
                float cut = id == "sfx_impact" ? 0.5f : 0.15f;
                for (int i = 0; i < b.Length; i++) { float t = i / (float)Rate; lp += (N() - lp) * cut; b[i] = (lp + MathF.Sin(2f * MathF.PI * 140f * t) * 0.5f) * MathF.Exp(-t * (id == "sfx_footstep" ? 45f : 28f)) * 0.9f; }
                break;
            }
            case "sfx_swing": case "sfx_throw":
            {
                b = Buf(0.28f); float lp = 0f;
                for (int i = 0; i < b.Length; i++) { float t = i / (float)Rate; float env = MathF.Sin(MathF.PI * t / 0.28f); lp += (N() - lp) * (0.05f + 0.25f * env); b[i] = lp * env * 1.2f; }
                break;
            }
            case "sfx_engine":
            {
                // 1 s seamless loop: harmonics of 40 Hz (exact integer cycles) + a little noise.
                b = Buf(1f); float lp = 0f;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    lp += (N() - lp) * 0.1f;
                    float s = 0f;
                    for (int h = 1; h <= 6; h++) s += MathF.Sin(2f * MathF.PI * 40f * h * t + h) / h;
                    b[i] = (s * 0.35f + lp * 0.25f) * (0.85f + 0.15f * MathF.Sin(2f * MathF.PI * 8f * t));
                }
                break;
            }
            case "sfx_physgun_hum":
            {
                b = Buf(1f);
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    b[i] = (MathF.Sin(2f * MathF.PI * 110f * t) * 0.4f + MathF.Sin(2f * MathF.PI * 220f * t) * 0.2f + MathF.Sin(2f * MathF.PI * 333f * t) * 0.1f)
                           * (0.8f + 0.2f * MathF.Sin(2f * MathF.PI * 4f * t));
                }
                break;
            }
            case "sfx_door":
            {
                b = Buf(0.25f); float lp = 0f;
                for (int i = 0; i < b.Length; i++) { float t = i / (float)Rate; lp += (N() - lp) * 0.2f; b[i] = (lp * 0.6f + MathF.Sin(2f * MathF.PI * 70f * t)) * MathF.Exp(-t * 18f); }
                break;
            }
            case "sfx_dry":
                b = Click(0.05f, 2500f, 90f, N);
                break;
            case "sfx_reload":
            {
                b = Buf(0.5f);
                foreach (float at in new[] { 0.0f, 0.22f, 0.4f })
                    for (int i = 0; i < Rate * 0.06f; i++)
                    {
                        int k = (int)(at * Rate) + i; if (k >= b.Length) break;
                        float t = i / (float)Rate;
                        b[k] += (N() * 0.6f + MathF.Sin(2f * MathF.PI * 1800f * t) * 0.4f) * MathF.Exp(-t * 80f);
                    }
                break;
            }
            case "sfx_spawn":
            {
                b = Buf(0.22f);
                for (int i = 0; i < b.Length; i++) { float t = i / (float)Rate; float f = 400f + 900f * t / 0.22f; b[i] = MathF.Sin(2f * MathF.PI * f * t) * MathF.Sin(MathF.PI * t / 0.22f) * 0.5f; }
                break;
            }
            case "sfx_weapon_switch": b = Click(0.08f, 1200f, 60f, N); break;
            case "sfx_ui_click": b = Click(0.04f, 1800f, 120f, N); break;
            case "sfx_ui_hover": b = Click(0.03f, 2600f, 160f, N, 0.2f); break;
            case "sfx_button": b = Click(0.12f, 900f, 35f, N); break;
            case "sfx_tool":
            {
                b = Buf(0.15f);
                for (int i = 0; i < b.Length; i++) { float t = i / (float)Rate; b[i] = (MathF.Sin(2f * MathF.PI * 1400f * t) + MathF.Sin(2f * MathF.PI * 2100f * t) * 0.5f) * MathF.Exp(-t * 30f) * 0.4f; }
                break;
            }
            case "sfx_pickup":
            {
                b = Buf(0.12f);
                for (int i = 0; i < b.Length; i++) { float t = i / (float)Rate; b[i] = MathF.Sin(2f * MathF.PI * (500f + 1500f * t) * t) * MathF.Exp(-t * 25f) * 0.5f; }
                break;
            }
            default: b = Buf(0.05f); break;
        }
        // Normalise to -1 dBFS-ish so every placeholder sits at a similar loudness.
        float peak = b.Length == 0 ? 0f : b.Max(MathF.Abs);
        if (peak > 1e-5f) for (int i = 0; i < b.Length; i++) b[i] *= 0.85f / peak;
        return b;
    }

    private static float[] Click(float len, float freq, float decay, Func<float> noise, float vol = 0.5f)
    {
        float[] b = Buf(len);
        for (int i = 0; i < b.Length; i++) { float t = i / (float)Rate; b[i] = (MathF.Sin(2f * MathF.PI * freq * t) * 0.7f + noise() * 0.3f) * MathF.Exp(-t * decay) * vol; }
        return b;
    }
}
