using System.Text.Json;

namespace Sandbox;

public enum GraphicsQuality { Low, Medium, High }

/// <summary>User settings, saved as JSON next to the user's app data (see <see cref="FilePath"/>).</summary>
public sealed class Settings
{
    public float MouseSensitivity { get; set; } = 1f;
    public float FieldOfView { get; set; } = 75f;
    public float MasterVolume { get; set; } = 0.8f;
    public float UiScale { get; set; } = 1f;
    public GraphicsQuality Graphics { get; set; } = GraphicsQuality.High;
    public bool ShowFps { get; set; }

    public static readonly (string Action, string Key)[] Keybinds =
    [
        ("Move", "W A S D"), ("Jump / fly up", "Space"), ("Run", "Shift"), ("Fly down (noclip)", "Ctrl"),
        ("Toggle noclip", "V"), ("Spawn menu", "Q / Tab (hold)"), ("Context menu", "C (hold)"),
        ("Weapons", "1–9 / Mouse wheel"), ("Primary / secondary", "LMB / RMB"), ("Reload", "R"),
        ("Use / enter vehicle", "E"), ("Undo", "Z"), ("FPS counter", "F3"), ("Pause menu", "Esc"),
    ];

    /// <summary>Overridable for tests.</summary>
    public static string FilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProwlSandbox", "settings.json");

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
        MouseSensitivity = Math.Clamp(MouseSensitivity, 0.1f, 4f);
        FieldOfView = Math.Clamp(FieldOfView, 60f, 110f);
        MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
        UiScale = Math.Clamp(UiScale, 0.75f, 1.5f);
        return this;
    }
}
