// Legacy code-built game objects are never destroyed while cached; ?./?? on them predates the 1.0 analyzer (revisit in stage 6).
#pragma warning disable PROWLEO001, PROWLEO002
using System.Reflection;
using System.Text.Json;
using Prowl.Runtime;
using Prowl.Runtime.Resources;
using Prowl.Vector;

namespace Sandbox;

/// <summary>
/// Asset registry. <c>assets/manifest.json</c> maps ids (vehicle_buggy, weapon_pistol, sfx_pistol_fire …)
/// to files under <c>assets/</c>. Anything missing falls back to the built-in placeholder.
/// </summary>
public static class AssetRegistry
{
    public sealed class Manifest
    {
        public Dictionary<string, string> Models { get; set; } = [];
        public Dictionary<string, string> Sounds { get; set; } = [];
        public Dictionary<string, float> ModelScale { get; set; } = [];
    }

    /// <summary>Game data folder: next to the exe when standalone (assets/); in the editor, GameData/ in the project root
    /// (outside Assets/ so the editor doesn't import the raw fonts/shaders/manifest).</summary>
#if PROWL_EDITOR
    public static string Root => Prowl.Editor.Projects.Project.Current is { } p
        ? Path.Combine(new DirectoryInfo(p.RootPath).ResolveLinkTarget(true)?.FullName ?? p.RootPath, "GameData") : Path.Combine(AppContext.BaseDirectory, "assets");
#else
    public static string Root => Path.Combine(AppContext.BaseDirectory, "assets");
#endif
    private static Manifest? _manifest;
    private static readonly HashSet<string> _failed = [];

    /// <summary>Animation clips imported with the last model for each id (for rigged view models).</summary>
    public static readonly Dictionary<string, List<AnimationClip>> Animations = [];

    public static Manifest Current
    {
        get
        {
            if (_manifest is not null) return _manifest;
            string path = Path.Combine(Root, "manifest.json");
            try
            {
                _manifest = File.Exists(path)
                    ? JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    : null;
            }
            catch (Exception e) { Console.WriteLine($"assets/manifest.json is invalid: {e.Message}"); }
            return _manifest ??= new Manifest();
        }
    }

    /// <summary>Absolute path for a sound id, or null if it isn't mapped or the file is missing.</summary>
    public static string? SoundPath(string id)
    {
        string rel = Current.Sounds.TryGetValue(id, out string? r) ? r : $"sounds/{id}.wav";
        string full = Path.Combine(Root, rel);
        if (File.Exists(full)) return full;
        return SoundGen.PathFor(id); // procedural placeholder
    }

    /// <summary>
    /// Instantiates the model mapped to <paramref name="id"/> (glTF/GLB/OBJ/FBX via Prowl's Clay importer)
    /// as a child of <paramref name="parent"/>. Returns false when there's no file, so the caller builds its placeholder.
    /// </summary>
    public static bool TryAttachModel(string id, GameObject parent, out GameObject? instance)
    {
        instance = null;
        // Manifest entry wins; otherwise fall back to the naming convention assets/models/<id>.(glb|gltf|fbx|obj).
        if (!Current.Models.TryGetValue(id, out string? rel))
            rel = new[] { "glb", "gltf", "fbx", "obj" }.Select(ext => Path.Combine("models", $"{id}.{ext}"))
                .FirstOrDefault(r => File.Exists(Path.Combine(Root, r)));
        if (rel is null) return false;
        string full = Path.Combine(Root, rel);
        if (!File.Exists(full)) return false;
        if (_failed.Contains(id)) return false;
        try
        {
            // Same importer the editor uses (Prowl.Clay: glTF/GLB, OBJ, FBX).
            var result = new Prowl.Runtime.AssetImporting.ModelImporter().Import(new FileInfo(full));
            instance = result.RootGO;
            if (instance is null) return false;
            Animations[id] = result.Animations;
            float scale = Current.ModelScale.TryGetValue(id, out float s) ? s : 1f;
            instance.Transform.LocalScale = new Float3(scale, scale, scale);
            instance.SetParent(parent, false);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Model '{id}' ({rel}) failed to load, using placeholder: {e.Message}");
            _failed.Add(id);
            return false;
        }
    }
}

/// <summary>
/// Fire-and-forget sound playback through Prowl's AudioSource. If the audio device failed to start
/// (e.g. a headless VM) or a sound file is missing, calls are silently ignored.
/// </summary>
public sealed class SoundSystem
{
    private readonly Dictionary<string, AudioClip?> _clips = [];
    private readonly List<AudioSource> _pool = [];
    private Scene? _scene;
    public float Volume { get; set; } = 0.8f;
    public bool Available { get; private set; }

    public void Init(Scene scene, GameObject listenerHost)
    {
        _scene = scene;
        Available = !Application.IsHeadless && DeviceReady();
        if (Available) listenerHost.AddComponent<AudioListener>();
        Console.WriteLine(Available ? "Audio ready." : "Audio unavailable (no device); sounds muted.");
    }

    private static bool DeviceReady()
    {
        try
        {
            FieldInfo? f = typeof(AudioSource).Assembly.GetType("Prowl.Runtime.Audio.AudioContext")
                ?.GetField("audioContext", BindingFlags.NonPublic | BindingFlags.Static);
            return f?.GetValue(null) is IntPtr p && p != IntPtr.Zero;
        }
        catch { return false; }
    }

    private AudioClip? Clip(string id)
    {
        if (_clips.TryGetValue(id, out AudioClip? c)) return c;
        string? path = AssetRegistry.SoundPath(id);
        try { c = path is null ? null : new AudioClip(path, streamFromDisk: false); }
        catch (Exception e) { Console.WriteLine($"Sound '{id}' failed to load: {e.Message}"); c = null; }
        return _clips[id] = c;
    }

    /// <summary>Plays a one-shot sound. With <paramref name="at"/> it's spatial (3D), otherwise 2D.</summary>
    public void Play(string id, float volume = 1f, Float3? at = null, float pitch = 1f)
    {
        if (!Available || _scene is null || Volume <= 0f) return;
        AudioClip? clip = Clip(id);
        if (clip is null) return;
        try
        {
            AudioSource? src = _pool.FirstOrDefault(s => !s.IsPlaying) ?? (_pool.Count < 32 ? NewSource() : null);
            if (src is null) return; // too many sounds at once
            src.Spatial = at is not null;
            if (at is { } p) src.Transform.Position = p;
            src.Clip = clip;
            src.Volume = volume * Volume;
            src.Pitch = pitch;
            src.Loop = false;
            src.Play();
        }
        catch (Exception e) { Console.WriteLine($"Sound '{id}' failed to play: {e.Message}"); }
    }

    /// <summary>Creates a looping source (engine, physgun hum). Returns null when audio is unavailable.</summary>
    public AudioSource? Loop(string id, GameObject host, float volume = 0.5f)
    {
        if (!Available) return null;
        AudioClip? clip = Clip(id);
        if (clip is null) return null;
        AudioSource src = host.AddComponent<AudioSource>();
        src.Clip = clip;
        src.Loop = true;
        src.Spatial = true;
        src.Volume = volume * Volume;
        src.MaxDistance = 40f;
        src.Play();
        return src;
    }

    private AudioSource NewSource()
    {
        GameObject go = new("SFX");
        AudioSource src = go.AddComponent<AudioSource>();
        src.MaxDistance = 60f;
        _scene!.Add(go);
        _pool.Add(src);
        return src;
    }
}
