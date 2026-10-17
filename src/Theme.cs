using Prowl.Scribe;
using Color = System.Drawing.Color;

namespace Sandbox;

/// <summary>
/// Shared design system: palette, spacing, type scale, fonts, and animation timings.
/// Every UI file pulls its look from here so the game stays visually consistent.
/// </summary>
public static class Theme
{
    // Palette: s&box-style frosted glass over a cool slate, one blue accent.
    public static readonly Color Backdrop = Color.FromArgb(150, 6, 8, 12);
    public static readonly Color Glass = Color.FromArgb(150, 20, 23, 30);       // main frosted panels
    public static readonly Color GlassTop = Color.FromArgb(175, 38, 42, 52);    // top of the glass gradient
    public static readonly Color Panel = Color.FromArgb(225, 22, 25, 31);
    public static readonly Color PanelRaised = Color.FromArgb(26, 255, 255, 255);
    public static readonly Color PanelSunken = Color.FromArgb(90, 0, 0, 0);
    public static readonly Color Hud = Color.FromArgb(110, 12, 14, 18);         // minimal in-game chips
    public static readonly Color Border = Color.FromArgb(34, 255, 255, 255);
    public static readonly Color BorderStrong = Color.FromArgb(70, 255, 255, 255);
    public static readonly Color Hover = Color.FromArgb(40, 255, 255, 255);
    public static readonly Color Press = Color.FromArgb(60, 255, 255, 255);
    public static readonly Color Accent = Color.FromArgb(255, 77, 163, 255);
    public static readonly Color AccentSoft = Color.FromArgb(110, 77, 163, 255);
    public static readonly Color Text = Color.FromArgb(255, 240, 243, 248);
    public static readonly Color TextMuted = Color.FromArgb(200, 210, 216, 228);
    public static readonly Color TextFaint = Color.FromArgb(130, 200, 206, 220);
    public static readonly Color Success = Color.FromArgb(255, 92, 214, 140);
    public static readonly Color Warning = Color.FromArgb(255, 250, 190, 80);
    public static readonly Color Danger = Color.FromArgb(255, 255, 99, 99);
    public static readonly Color Shadow = Color.FromArgb(90, 0, 0, 0);

    // Spacing (logical pixels before UI scale).
    // Compact, GMod-dense spacing (s&box glass look kept).
    public const float Xs = 3f, Sm = 6f, Md = 9f, Lg = 12f, Xl = 16f, Xxl = 22f;
    public const float Radius = 12f, RadiusSm = 8f, RadiusXs = 5f;
    public const float Blur = 18f;

    // Type scale.
    public const float Micro = 10f, Caption = 11f, Body = 12.5f, Label = 13.5f, Title = 16f, Heading = 21f, Display = 52f;
    /// <summary>Global density: everything is laid out at this fraction of the 1280x720 reference size.</summary>
    public const float Density = 0.92f;

    // Animation.
    public const float OpenSpeed = 7f;   // 1/s, menu open/close
    public const float ToastLife = 2.6f, ToastFade = 0.35f;
    public const float SpringStiffness = 420f, SpringDamping = 26f;

    /// <summary>User UI scale multiplier from Settings.</summary>
    public static float UserScale { get; set; } = 1f;

    private static FontFile? _regular, _semibold, _bold;
    public static FontFile Regular => _regular ??= Load("Inter-Regular.ttf");
    public static FontFile SemiBold => _semibold ??= Load("Inter-SemiBold.ttf");
    public static FontFile Bold => _bold ??= Load("Inter-Bold.ttf");

    /// <summary>UI scale relative to a 1280x720 reference, so the UI grows with the window.</summary>
    public static float Scale { get; private set; } = 1f;

    public static void UpdateScale(float width, float height)
        => Scale = Math.Clamp(MathF.Min(width / 1280f, height / 720f), 0.75f, 2.5f) * UserScale * Density;

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
