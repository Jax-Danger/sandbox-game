using System.Text.Json;
using Prowl.Runtime;

namespace Sandbox;

public sealed record Achievement(string Id, string Name, string Description, bool Roleplay = false);

/// <summary>
/// Lightweight achievements: polled conditions plus toast-driven events, unlocked once, persisted next to
/// settings.json. Headless runs never touch the real file unless a test points <see cref="FilePath"/> elsewhere.
/// </summary>
public sealed class Achievements
{
    public static readonly Achievement[] All =
    [
        new("first_spawn", "First Steps", "Spawn a prop"),
        new("builder", "Builder", "Have 25 props in the world at once"),
        new("architect", "Architect", "Have 100 props in the world at once"),
        new("welder", "Stuck Together", "Weld two props"),
        new("balloonist", "Up, Up and Away", "Attach a balloon"),
        new("crash_test", "Crash Test Dummy", "Die for the first time"),
        new("demolition", "Demolition Expert", "Cause 5 explosions"),
        new("road_trip", "Road Trip", "Drive a vehicle"),
        new("photographer", "Photographer", "Save a screenshot (F12)"),
        new("mad_scientist", "Mad Scientist", "Change the world's gravity"),
        new("night_owl", "Night Owl", "Stay up past midnight"),
        new("hacker", "Hacker", "Run a console command"),
        new("first_blood", "First Blood", "Take down an NPC"),
        new("tycoon", "Tycoon", "Hold $2,000 between wallet and bank", Roleplay: true),
        new("lawman", "Long Arm of the Law", "Make an arrest", Roleplay: true),
    ];

    public static string FilePath { get; set; } = Path.Combine(Path.GetDirectoryName(Settings.FilePath) ?? ".", "achievements.json");
    private static readonly string DefaultPath = FilePath;

    private readonly HashSet<string> _unlocked = [];
    private readonly Queue<string> _events = new();
    private float _poll;
    public IReadOnlyCollection<string> Unlocked => _unlocked;
    public bool Has(string id) => _unlocked.Contains(id);
    public event Action<Achievement>? OnUnlock;

    private bool Persist => !Application.IsHeadless || FilePath != DefaultPath;

    public void Load()
    {
        _unlocked.Clear();
        if (!Persist) return;
        try
        {
            if (File.Exists(FilePath) && JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath)) is { } ids)
                foreach (string id in ids) if (All.Any(a => a.Id == id)) _unlocked.Add(id);
        }
        catch (Exception e) { Console.WriteLine($"Achievements could not be read: {e.Message}"); }
    }

    private void Save()
    {
        if (!Persist) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_unlocked.OrderBy(x => x).ToList()));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception e) { Console.WriteLine($"Achievements could not be saved: {e.Message}"); }
    }

    /// <summary>Feed toast texts in; they double as gameplay events ("Welded …", "Balloon …").</summary>
    public void Observe(string message)
    {
        if (message.StartsWith("Spawned")) _events.Enqueue("first_spawn");
        else if (message.StartsWith("Welded")) _events.Enqueue("welder");
        else if (message.StartsWith("Balloon")) _events.Enqueue("balloonist");
        else if (message.StartsWith("Saved screenshot")) _events.Enqueue("photographer");
    }

    public void Event(string id) => _events.Enqueue(id);

    public bool Unlock(string id)
    {
        Achievement? a = All.FirstOrDefault(x => x.Id == id);
        if (a is null || !_unlocked.Add(id)) return false;
        Save();
        OnUnlock?.Invoke(a);
        return true;
    }

    public void Tick(SandboxGame g, float dt)
    {
        while (_events.TryDequeue(out string? id)) Unlock(id);
        if (g.Session is null && g.SelfTest is null) return;
        _poll -= dt;
        if (_poll > 0f) return;
        _poll = 0.5f;
        int props = g.Props.Props.Count;
        if (props >= 25) Unlock("builder");
        if (props >= 100) Unlock("architect");
        if (g.Health.Deaths >= 1) Unlock("crash_test");
        if (g.Fx.Explosions >= 5) Unlock("demolition");
        if (g.InVehicle) Unlock("road_trip");
        if (MathF.Abs(g.World.Physics.Gravity.Y + 9.81f) > 0.5f) Unlock("mad_scientist");
        if (g.DayNight.Enabled && g.DayNight.TimeOfDay is > 0.0f and < 0.03f) Unlock("night_owl");
        if (g.IsRoleplay)
        {
            if (g.Roleplay.Money + g.Roleplay.Bank >= 2000) Unlock("tycoon");
            if (g.Roleplay.Arrests >= 1) Unlock("lawman");
        }
    }
}
