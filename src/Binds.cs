using Prowl.Runtime;

namespace Sandbox;

/// <summary>
/// Rebindable actions. Defaults live here; the player's overrides persist in <see cref="Settings.Binds"/>.
/// Game code asks <c>Binds.Down("use")</c> instead of hard-coding a KeyCode.
/// </summary>
public static class Binds
{
    public static readonly (string Id, string Label, KeyCode Default)[] Actions =
    [
        ("forward", "Move forward", KeyCode.W), ("back", "Move back", KeyCode.S),
        ("left", "Strafe left", KeyCode.A), ("right", "Strafe right", KeyCode.D),
        ("jump", "Jump / fly up", KeyCode.Space), ("use", "Use / enter vehicle", KeyCode.E),
        ("reload", "Reload", KeyCode.R), ("flashlight", "Flashlight", KeyCode.F),
        ("noclip", "Toggle noclip", KeyCode.V), ("undo", "Undo", KeyCode.Z),
        ("zoom", "Zoom (hold)", KeyCode.B), ("spawnmenu", "Spawn menu (hold)", KeyCode.Q),
        ("context", "Context menu (hold)", KeyCode.C), ("jobs", "Jobs menu (Roleplay)", KeyCode.F4),
        ("fps", "FPS counter", KeyCode.F3),
    ];

    public static Settings? Source;

    public static KeyCode Get(string id)
    {
        if (Source?.Binds is { } b && b.TryGetValue(id, out KeyCode k)) return k;
        foreach (var a in Actions) if (a.Id == id) return a.Default;
        return KeyCode.Unknown;
    }

    public static void Set(string id, KeyCode k)
    {
        if (Source is null) return;
        Source.Binds ??= [];
        Source.Binds[id] = k;
        Source.Save();
    }

    public static bool Down(string id) => !KeyCapture.Active && !Typing && Input.GetKeyDown(Get(id));
    public static bool Held(string id) => !KeyCapture.Active && !Typing && Input.GetKey(Get(id));
    /// <summary>True while a text box (console) owns the keyboard.</summary>
    public static bool Typing { get; set; }
    public static string Name(string id) => KeyName(Get(id));
    public static string KeyName(KeyCode k) => k.ToString().Replace("Keypad", "Num ").Replace("Number", "");
}

/// <summary>Waits for the next key press and hands it to a callback (Esc cancels). Drives every key-picker widget.</summary>
public static class KeyCapture
{
    public static string? ActiveId { get; private set; }
    private static Action<KeyCode>? _cb;
    private static int _armedFrame;
    /// <summary>True on the frame a capture ended, so Esc/keys don't also trigger game actions.</summary>
    public static bool JustEnded { get; private set; }
    public static bool Active => ActiveId is not null || JustEnded;
    private static readonly KeyCode[] All = Enum.GetValues<KeyCode>().Where(k => k != KeyCode.Unknown).Distinct().ToArray();

    public static void Begin(string id, Action<KeyCode> cb) { ActiveId = id; _cb = cb; _armedFrame = 2; }

    /// <summary>Call once per frame before input handling.</summary>
    public static void Poll()
    {
        JustEnded = false;
        if (ActiveId is null) return;
        if (_armedFrame-- > 0) return; // ignore the click/key that opened it
        foreach (KeyCode k in All)
        {
            if (!Input.GetKeyDown(k)) continue;
            if (k != KeyCode.Escape) _cb?.Invoke(k);
            ActiveId = null; _cb = null; JustEnded = true;
            return;
        }
    }
}
