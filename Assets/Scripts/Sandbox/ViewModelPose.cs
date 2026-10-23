using System.Text.Json;
using Prowl.Runtime;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Hand-editable first-person arms pose for one view model (physgun, toolgun, pistol, SMG, shotgun, bare hands, ...).
/// It sits on the view model root object (<c>vm_&lt;id&gt;</c>, under the player camera) while playing; YBotArms reads it
/// every frame, so inspector edits show immediately. Press <b>Save Pose</b> to write
/// <c>GameData/viewmodels/&lt;id&gt;.pose.json</c>; that file is loaded every time the view model is built, so edits survive
/// leaving Play (inspector values on play-mode objects are thrown away when Play stops).
/// Positions are metres, rotations degrees, finger curls degrees per joint (X = knuckle, Y = middle, Z = tip).
/// </summary>
public sealed class ViewModelPose : Component
{
    [Header("Pose file")]
    [ReadOnly, Tooltip("View model id; saves to GameData/viewmodels/<id>.pose.json")] public string WeaponId = "";
    [ReadOnly] public string Status = "";

    [Header("Source animation (arm base pose)")]
    [Tooltip("Y Bot clip under Assets/Resources/Player/Animations, e.g. RifleAimIdle, Idle, RifleFire")] public string SourceClip = "RifleAimIdle";
    [Tooltip("Seconds into the clip to freeze on; -1 = play the clip (breathing idle)")] public float SourceFrame = -1f;

    [Header("Arms root")]
    [Tooltip("Shoulder midpoint relative to the view model root (x right, y up, z forward). Typical -0.4..0.4")] public Float3 ArmsPosition = new(0f, -0.24f, -0.03f);
    [Tooltip("Extra rotation of the whole arms rig, degrees (pitch, yaw, roll)")] public Float3 ArmsRotation = Float3.Zero;
    [Range(0.5f, 1.5f), Tooltip("Arm and hand size; 1 = real Y Bot size")] public float ArmsScale = 0.85f;
    [Tooltip("Bare hands only: looking down past this pitch the hands sink out of view")] public float HideHandsAbovePitch = 35f;

    [Header("Right hand")]
    [Tooltip("Where the right wrist goes, relative to the weapon's grip point (hand_r bone), in the grip's frame. Typical -0.15..0.15")] public Float3 RightGripSocket = Float3.Zero;
    [Tooltip("Extra right wrist offset in view space (x right, y up, z forward)")] public Float3 RightWristOffset = Float3.Zero;
    [Tooltip("Extra right wrist rotation, degrees (in the hand's own axes)")] public Float3 RightWristRotation = Float3.Zero;
    [Tooltip("Curl per joint, degrees (knuckle, middle, tip). 0 = straight, ~90 = closed")] public Float3 RightThumb, RightIndex, RightMiddle, RightRing, RightPinky;

    [Header("Left hand")]
    [Tooltip("True: the left hand reaches the weapon's foregrip; false: it hangs free (bare hands, one-handed tools)")] public bool LeftOnWeapon;
    [Tooltip("Where the left wrist goes, relative to the weapon's foregrip point (hand_l bone), in that grip's frame")] public Float3 LeftForegripSocket = Float3.Zero;
    [Tooltip("Extra left wrist offset in view space (x right, y up, z forward)")] public Float3 LeftWristOffset = Float3.Zero;
    [Tooltip("Extra left wrist rotation, degrees (in the hand's own axes)")] public Float3 LeftWristRotation = Float3.Zero;
    public Float3 LeftThumb, LeftIndex, LeftMiddle, LeftRing, LeftPinky;

    /// <summary>Set to true by YBotArms once defaults (or the saved file) are in.</summary>
    [HideInInspector] public bool Initialized;
    /// <summary>Reset To Defaults: rebuild from the built-in values without reading the saved file.</summary>
    [HideInInspector] public bool SkipFileOnce;

    public static string PathFor(string id) => Path.Combine(AssetRegistry.Root, "viewmodels", $"{id}.pose.json");

    [Button("Save Pose")]
    public void SavePose()
    {
        try
        {
            string path = PathFor(WeaponId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(ToData(), new JsonSerializerOptions { WriteIndented = true }));
            Status = $"Saved {DateTime.Now:HH:mm:ss} to {path}";
        }
        catch (Exception e) { Status = $"Save failed: {e.Message}"; }
        Console.WriteLine($"[pose] {Status}");
    }

    [Button("Reload Saved")]
    public void ReloadSaved() => Status = TryLoad() ? "Reloaded from file" : "No saved pose file";

    [Button("Reset To Defaults")]
    public void ResetToDefaults() { Initialized = false; SkipFileOnce = true; Status = "Defaults restored (not saved yet)"; }

    public bool TryLoad()
    {
        try
        {
            string path = PathFor(WeaponId);
            if (!File.Exists(path)) return false;
            if (JsonSerializer.Deserialize<Data>(File.ReadAllText(path)) is not { } d) return false;
            FromData(d);
            return true;
        }
        catch (Exception e) { Status = $"Load failed: {e.Message}"; return false; }
    }

    // ── JSON (Float3 as [x, y, z]) ──
    public sealed class Data
    {
        public string SourceClip { get; set; } = "RifleAimIdle";
        public float SourceFrame { get; set; } = -1f;
        public float[] ArmsPosition { get; set; } = [0, 0, 0];
        public float[] ArmsRotation { get; set; } = [0, 0, 0];
        public float ArmsScale { get; set; } = 0.85f;
        public float HideHandsAbovePitch { get; set; } = 35f;
        public bool LeftOnWeapon { get; set; }
        public Dictionary<string, float[]> Vectors { get; set; } = [];
    }

    private static float[] A(Float3 v) => [v.X, v.Y, v.Z];
    private static Float3 V(float[]? a, Float3 fallback) => a is { Length: 3 } ? new Float3(a[0], a[1], a[2]) : fallback;

    private Data ToData() => new()
    {
        SourceClip = SourceClip, SourceFrame = SourceFrame, ArmsPosition = A(ArmsPosition), ArmsRotation = A(ArmsRotation),
        ArmsScale = ArmsScale, HideHandsAbovePitch = HideHandsAbovePitch, LeftOnWeapon = LeftOnWeapon,
        Vectors = new()
        {
            ["RightGripSocket"] = A(RightGripSocket), ["RightWristOffset"] = A(RightWristOffset), ["RightWristRotation"] = A(RightWristRotation),
            ["RightThumb"] = A(RightThumb), ["RightIndex"] = A(RightIndex), ["RightMiddle"] = A(RightMiddle), ["RightRing"] = A(RightRing), ["RightPinky"] = A(RightPinky),
            ["LeftForegripSocket"] = A(LeftForegripSocket), ["LeftWristOffset"] = A(LeftWristOffset), ["LeftWristRotation"] = A(LeftWristRotation),
            ["LeftThumb"] = A(LeftThumb), ["LeftIndex"] = A(LeftIndex), ["LeftMiddle"] = A(LeftMiddle), ["LeftRing"] = A(LeftRing), ["LeftPinky"] = A(LeftPinky),
        },
    };

    private void FromData(Data d)
    {
        SourceClip = d.SourceClip; SourceFrame = d.SourceFrame; ArmsPosition = V(d.ArmsPosition, ArmsPosition); ArmsRotation = V(d.ArmsRotation, ArmsRotation);
        ArmsScale = d.ArmsScale; HideHandsAbovePitch = d.HideHandsAbovePitch; LeftOnWeapon = d.LeftOnWeapon;
        Float3 G(string k, Float3 f) => V(d.Vectors.GetValueOrDefault(k), f);
        RightGripSocket = G("RightGripSocket", RightGripSocket); RightWristOffset = G("RightWristOffset", RightWristOffset); RightWristRotation = G("RightWristRotation", RightWristRotation);
        RightThumb = G("RightThumb", RightThumb); RightIndex = G("RightIndex", RightIndex); RightMiddle = G("RightMiddle", RightMiddle); RightRing = G("RightRing", RightRing); RightPinky = G("RightPinky", RightPinky);
        LeftForegripSocket = G("LeftForegripSocket", LeftForegripSocket); LeftWristOffset = G("LeftWristOffset", LeftWristOffset); LeftWristRotation = G("LeftWristRotation", LeftWristRotation);
        LeftThumb = G("LeftThumb", LeftThumb); LeftIndex = G("LeftIndex", LeftIndex); LeftMiddle = G("LeftMiddle", LeftMiddle); LeftRing = G("LeftRing", LeftRing); LeftPinky = G("LeftPinky", LeftPinky);
    }
}
