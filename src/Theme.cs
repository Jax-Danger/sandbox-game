using Prowl.Scribe;
using Color = System.Drawing.Color;

namespace Sandbox;

/// <summary>
/// Shared design system: palette, spacing, type scale, fonts, and animation timings.
/// Every UI file pulls its look from here so the game stays visually consistent.
/// </summary>
public static class Theme
{
    // Palette (dark, GMod-like slate with a blue accent).
    public static readonly Color Backdrop = Color.FromArgb(150, 6, 8, 12);
    public static readonly Color Panel = Color.FromArgb(232, 24, 27, 33);
    public static readonly Color PanelRaised = Color.FromArgb(240, 34, 38, 46);
    public static readonly Color PanelSunken = Color.FromArgb(235, 17, 19, 24);
    public static readonly Color Hud = Color.FromArgb(200, 18, 20, 26);
    public static readonly Color Border = Color.FromArgb(60, 255, 255, 255);
    public static readonly Color Hover = Color.FromArgb(255, 48, 54, 66);
    public static readonly Color Accent = Color.FromArgb(255, 64, 156, 255);
    public static readonly Color AccentSoft = Color.FromArgb(255, 36, 72, 120);
    public static readonly Color Text = Color.FromArgb(255, 236, 239, 244);
    public static readonly Color TextMuted = Color.FromArgb(255, 150, 158, 172);
    public static readonly Color TextFaint = Color.FromArgb(255, 98, 106, 120);
    public static readonly Color Success = Color.FromArgb(255, 88, 200, 120);
    public static readonly Color Warning = Color.FromArgb(255, 245, 180, 70);
    public static readonly Color Danger = Color.FromArgb(255, 235, 85, 85);
    public static readonly Color Shadow = Color.FromArgb(120, 0, 0, 0);

    // Spacing (logical pixels before UI scale).
    public const float Xs = 4f, Sm = 8f, Md = 12f, Lg = 16f, Xl = 24f, Xxl = 32f;
    public const float Radius = 10f, RadiusSm = 6f;

    // Type scale.
    public const float Caption = 12f, Body = 14f, Label = 16f, Title = 20f, Heading = 28f, Display = 56f;

    // Animation.
    public const float OpenSpeed = 9f;   // 1/s, menu open/close
    public const float ToastLife = 3.2f, ToastFade = 0.35f;

    private static FontFile? _regular, _semibold, _bold;
    public static FontFile Regular => _regular ??= Load("Inter-Regular.ttf");
    public static FontFile SemiBold => _semibold ??= Load("Inter-SemiBold.ttf");
    public static FontFile Bold => _bold ??= Load("Inter-Bold.ttf");

    /// <summary>UI scale relative to a 1280x720 reference, so the UI grows with the window.</summary>
    public static float Scale { get; private set; } = 1f;

    public static void UpdateScale(float width, float height)
        => Scale = Math.Clamp(MathF.Min(width / 1280f, height / 720f), 0.75f, 2.5f);

    public static float S(float v) => v * Scale;

    public static Color WithAlpha(Color c, float alphaMul)
        => Color.FromArgb((int)Math.Clamp(c.A * alphaMul, 0, 255), c.R, c.G, c.B);

    public static Prowl.Vector.Color ToVector(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

    /// <summary>Smoothstep easing for open/close animations.</summary>
    public static float Ease(float t) { t = Math.Clamp(t, 0f, 1f); return t * t * (3f - 2f * t); }

    private static FontFile Load(string file)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "assets", "fonts", file);
        if (File.Exists(path))
            return new FontFile(File.ReadAllBytes(path));
        // Fallback: Prowl's built-in Geist font.
        return Prowl.Runtime.Resources.FontAsset.LoadDefault().FontFile;
    }
}
