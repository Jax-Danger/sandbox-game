using System.Text.Json;

namespace Sandbox;

public enum GraphicsQuality { Low, Medium, High }
public enum WindowMode { Windowed, Borderless, Fullscreen }

/// <summary>User settings, saved as JSON next to the user's app data (see <see cref="FilePath"/>).</summary>
public sealed class Settings
{
    public float MouseSensitivity { get; set; } = 1f;
    public float FieldOfView { get; set; } = 75f;
    /// <summary>Field of view the first-person view model is drawn at, independent of the world FOV.</summary>
    public float ViewModelFov { get; set; } = 68f;
    /// <summary>Players allowed to touch your props under prop protection.</summary>
    public List<string> Buddies { get; set; } = [];
    /// <summary>Getting-started tutorial progress (steps completed).</summary>
    public int TutorialStep { get; set; }
    /// <summary>Prop protection: off, rp (Roleplay only) or always.</summary>
    public string PropProtection { get; set; } = "rp";
    /// <summary>World time scale (Utilities slider); 1 = real time.</summary>
    public float TimeScale { get; set; } = 1f;
    public float MasterVolume { get; set; } = 0.8f;
    public float UiScale { get; set; } = 1f;
    /// <summary>First-run default tuned for mid-range GPUs: soft shadows, FXAA, bloom, no GTAO.</summary>
    public GraphicsQuality Graphics { get; set; } = GraphicsQuality.Medium;
    public bool ShowFps { get; set; }
    public bool ShowHints { get; set; } = true;
    public bool DamageNumbers { get; set; } = true;
    public List<string> SeenHints { get; set; } = [];
    public WindowMode WindowMode { get; set; } = WindowMode.Windowed;
    public int ResolutionWidth { get; set; } = 1280;
    public int ResolutionHeight { get; set; } = 720;
    /// <summary>Applied at start-up (the swap interval lives on the render thread).</summary>
    public bool VSync { get; set; } = true;
    /// <summary>0 = unlimited. Default 144 (with VSync) so the CPU isn't burned on frames nobody sees.</summary>
    public int FrameCap { get; set; } = 144;
    /// <summary>Battery / quiet-fans mode: 60 FPS cap, 15 FPS in the background, cheaper graphics.</summary>
    public bool LowPower { get; set; }
    /// <summary>Bumped when a default changes and old files need migrating.</summary>
    public int Version { get; set; }
    public bool FirstRunDone { get; set; }
    /// <summary>Rebound actions (id -> key); missing ids use <see cref="Binds"/> defaults.</summary>
    public Dictionary<string, Prowl.Runtime.KeyCode>? Binds { get; set; }

    public static readonly (int W, int H)[] Resolutions = [(1280, 720), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160)];
    public static readonly int[] FrameCaps = [0, 30, 60, 120, 144, 240];

    /// <summary>Overridable for tests.</summary>
    public static string FilePath { get; set; } = Path.Combine(
        DataPaths.Root, "settings.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return (JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings()).Clamped();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Settings could not be read, using defaults: {e.Message}");
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Clamped(), Json));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Settings could not be saved: {e.Message}");
        }
    }

    public Settings Clamped()
    {
        Buddies = (Buddies ?? []).Where(b => b is { Length: > 0 and <= 32 }).Distinct(StringComparer.OrdinalIgnoreCase).Take(64).ToList();
        PropProtection = PropProtection is "off" or "rp" or "always" ? PropProtection : "rp";
        SeenHints = (SeenHints ?? []).Where(x => x is not null).Distinct().Take(100).ToList();
        MouseSensitivity = Math.Clamp(MouseSensitivity, 0.1f, 4f);
        FieldOfView = Math.Clamp(FieldOfView, 60f, 110f);
        ViewModelFov = float.IsFinite(ViewModelFov) ? Math.Clamp(ViewModelFov, 50f, 90f) : 68f;
        MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
        UiScale = Math.Clamp(UiScale, 0.75f, 1.5f);
        ResolutionWidth = Math.Clamp(ResolutionWidth, 640, 7680);
        ResolutionHeight = Math.Clamp(ResolutionHeight, 360, 4320);
        if (Version < 2) { if (FrameCap == 0) FrameCap = 144; Version = 2; } // old default was unlimited
        FrameCap = Math.Clamp(FrameCap, 0, 1000);
        return this;
    }
}
