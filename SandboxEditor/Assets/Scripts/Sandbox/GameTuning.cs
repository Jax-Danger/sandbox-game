using Prowl.Runtime;

namespace Sandbox;

/// <summary>
/// Game tuning with editable fields: put it next to SandboxHost. Values are pushed into the game every frame while
/// playing, so inspector edits take effect live (and are kept in the scene). The standalone exe uses the defaults.
/// </summary>
public sealed class GameTuning : Component
{
    [Header("Lighting")]
    [Tooltip("Scales the full-day sun intensity (night unchanged).")] public float SunScale = 0.55f;
    [Tooltip("Scales the full-day ambient light (night unchanged).")] public float AmbientScale = 0.7f;
    [Header("Day / night")]
    [Tooltip("0 = midnight, 0.25 sunrise, 0.5 noon, 0.75 sunset. Applied when play starts.")] public float StartTimeOfDay = 0.42f;
    public bool DayCycle;
    public float DayLengthSeconds = 600f;
    [Header("Player")]
    public float Gravity = 20f;
    public float WalkSpeed = 5f, RunSpeed = 9f, JumpSpeed = 6.5f, CrouchSpeed = 2.3f;
    [Header("Maps")]
    [Tooltip("Load maps from the baked scenes in MapLibrary (off = build them in code).")] public bool UseBakedMaps = true;

    private string? _applied;

    /// <summary>Pushes the values when play starts and whenever they change in the inspector (so in-game toggles and the
    /// console still work in between).</summary>
    public override void Update()
    {
        if (SandboxHost.Current is not { } g) return;
        string now = string.Join("|", SunScale, AmbientScale, DayCycle, DayLengthSeconds, Gravity, WalkSpeed, RunSpeed, JumpSpeed, CrouchSpeed, UseBakedMaps);
        if (_applied == now) return;
        if (_applied is null) g.DayNight.TimeOfDay = StartTimeOfDay;
        _applied = now;
        DayNightCycle.SunScale = SunScale; DayNightCycle.AmbientScale = AmbientScale;
        g.DayNight.Enabled = DayCycle; g.DayNight.DayLengthSeconds = MathF.Max(10f, DayLengthSeconds);
        Player.Gravity = Gravity; Player.WalkSpeed = WalkSpeed; Player.RunSpeed = RunSpeed; Player.JumpSpeed = JumpSpeed; Player.CrouchSpeed = CrouchSpeed;
        Environment.SetEnvironmentVariable("SANDBOX_MAPSCENES", UseBakedMaps ? null : "0");
    }
}
