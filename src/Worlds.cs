using System.Diagnostics;
using System.Text.Json;
using Prowl.Runtime;
using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;

namespace Sandbox;

/// <summary>World-creation modifiers, stored in session.json.</summary>
public sealed class WorldModifiers
{
    public float Gravity { get; set; } = 9.81f;
    public bool FallDamage { get; set; } = true;
    public int StartingMoney { get; set; } = 500;
    public int PropLimit { get; set; } = 400;
    public bool DayNight { get; set; }
    public bool Npcs { get; set; } = true;
    /// <summary>Roleplay: sandbox props cost money (false = free, true = $ per prop).</summary>
    public bool PaidSpawning { get; set; } = true;
}

/// <summary>session.json: metadata shown in the Worlds list.</summary>
public sealed class SessionMeta
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public string Name { get; set; } = "New World";
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime LastPlayed { get; set; } = DateTime.Now;
    public string Mode { get; set; } = "Sandbox";          // Sandbox | Roleplay
    public string Map { get; set; } = "flatgrass";
    /// <summary>File name of the imported custom map inside the session folder.</summary>
    public string? CustomMap { get; set; }
    public WorldModifiers Modifiers { get; set; } = new();
    public int Seed { get; set; } = Random.Shared.Next();
    public string GameVersion { get; set; } = Worlds.GameVersion;
    public int PropCount { get; set; }
    public double PlaySeconds { get; set; }
}

/// <summary>player.json: where the player was and what they carried.</summary>
public sealed class PlayerSave
{
    public int Version { get; set; } = 1;
    public float[] Feet { get; set; } = [];
    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public bool Noclip { get; set; }
    public string? Weapon { get; set; }
    public float Health { get; set; } = 100f;
    public float Armor { get; set; }
    public int Money { get; set; }
    public string Job { get; set; } = "citizen";
    public List<string> Laws { get; set; } = [];
    public int Bank { get; set; }
    public List<string> OwnedDoors { get; set; } = [];
    public List<string> LockedDoors { get; set; } = [];
    public float JailTimer { get; set; }
    public float WantedTimer { get; set; }
    public float Hunger { get; set; } = 100f;
}

public sealed record WorldEntry(string Folder, SessionMeta Meta)
{
    public string Icon => Path.Combine(Folder, "icon.png");
}

/// <summary>
/// Session folders under the app-data saves dir, Minecraft-style: saves/&lt;folder&gt;/{session,world,player}.json + icon.png.
/// All writes are atomic (temp file + rename) and keep one .bak of the previous version.
/// </summary>
public static class Worlds
{
    public const string GameVersion = "0.3.0";
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string Root { get; set; } = Environment.GetEnvironmentVariable("SANDBOX_SAVES") is { Length: > 0 } env ? env : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProwlSandbox", "saves");

    public static List<WorldEntry> List()
    {
        var list = new List<WorldEntry>();
        if (!Directory.Exists(Root)) return list;
        foreach (string dir in Directory.GetDirectories(Root))
        {
            string meta = Path.Combine(dir, "session.json");
            if (!File.Exists(meta)) continue;
            try
            {
                SessionMeta? m = JsonSerializer.Deserialize<SessionMeta>(File.ReadAllText(meta));
                if (m is not null) list.Add(new WorldEntry(dir, m));
            }
            catch (Exception e) { Log.Warn($"Skipping broken world {dir}: {e.Message}"); }
        }
        return list.OrderByDescending(w => w.Meta.LastPlayed).ToList();
    }

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

    /// <summary>File-system-safe base name (no uniqueness suffix): letters/digits/space/-/_ only, no Windows device names.</summary>
    public static string BaseFolderName(string name)
    {
        var chars = name.Trim().Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' ? c : '_').ToArray();
        string s = new string(chars).Trim();
        if (s.Length == 0 || s.All(c => c == '_')) s = "World";
        if (s.Length > 48) s = s[..48].TrimEnd();
        if (Reserved.Contains(s)) s = "World " + s;
        return s;
    }

    /// <summary>A free folder name for a world; <paramref name="current"/> (the world's own folder) counts as free.</summary>
    public static string SafeFolderName(string name, string? current = null)
    {
        string s = BaseFolderName(name);
        string candidate = s; int n = 2;
        while (Directory.Exists(Path.Combine(Root, candidate)) && !string.Equals(candidate, current, StringComparison.Ordinal)) candidate = $"{s} ({n++})";
        return candidate;
    }

    /// <summary>Creates the folder and session.json; copies a custom map file into it.</summary>
    public static WorldEntry Create(SessionMeta meta, string? customMapSource = null)
    {
        Directory.CreateDirectory(Root);
        string folder = Path.Combine(Root, SafeFolderName(meta.Name));
        Directory.CreateDirectory(folder);
        if (meta.Map == "custom" && customMapSource is not null && File.Exists(customMapSource))
        {
            string dest = Path.Combine(folder, "map" + Path.GetExtension(customMapSource).ToLowerInvariant());
            File.Copy(customMapSource, dest, overwrite: true);
            // glTF can reference side files (.bin, textures): copy siblings too.
            if (Path.GetExtension(customMapSource).Equals(".gltf", StringComparison.OrdinalIgnoreCase))
                foreach (string f in Directory.GetFiles(Path.GetDirectoryName(customMapSource)!))
                    if (!f.Equals(customMapSource) && Path.GetExtension(f).ToLowerInvariant() is ".bin" or ".png" or ".jpg" or ".jpeg")
                        File.Copy(f, Path.Combine(folder, Path.GetFileName(f)), true);
            meta.CustomMap = Path.GetFileName(dest);
        }
        meta.Created = meta.LastPlayed = DateTime.Now;
        WriteMeta(folder, meta);
        return new WorldEntry(folder, meta);
    }

    public static void WriteMeta(string folder, SessionMeta meta) => WriteAtomic(Path.Combine(folder, "session.json"), JsonSerializer.Serialize(meta, Json));

    public static WorldEntry Rename(WorldEntry w, string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0) return w;
        w.Meta.Name = newName;
        string folder = w.Folder;
        string current = Path.GetFileName(folder);
        // Keep the folder if the new name maps to it already (e.g. same name, or "World (2)" renamed to "World").
        string target = BaseFolderName(newName) == current || current.StartsWith(BaseFolderName(newName) + " (") ? folder : Path.Combine(Root, SafeFolderName(newName, current));
        try { if (!Path.GetFileName(folder).Equals(Path.GetFileName(target))) { Directory.Move(folder, target); folder = target; } }
        catch (Exception e) { Log.Warn($"Rename folder failed, keeping folder name: {e.Message}"); }
        WriteMeta(folder, w.Meta);
        return new WorldEntry(folder, w.Meta);
    }

    public static void Delete(WorldEntry w)
    {
        // Only ever delete folders inside our own saves root.
        string full = Path.GetFullPath(w.Folder);
        string root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.Ordinal) || full.Length <= root.Length) throw new InvalidOperationException("Refusing to delete outside the saves folder.");
        Directory.Delete(full, recursive: true);
    }

    public static WorldEntry Duplicate(WorldEntry w)
    {
        string copyName = w.Meta.Name + " (copy)";
        string folder = Path.Combine(Root, SafeFolderName(copyName));
        Directory.CreateDirectory(folder);
        foreach (string f in Directory.GetFiles(w.Folder)) File.Copy(f, Path.Combine(folder, Path.GetFileName(f)));
        SessionMeta meta = JsonSerializer.Deserialize<SessionMeta>(JsonSerializer.Serialize(w.Meta))!;
        meta.Name = copyName; meta.Created = DateTime.Now;
        WriteMeta(folder, meta);
        return new WorldEntry(folder, meta);
    }

    public static void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string opener = OperatingSystem.IsWindows() ? "explorer.exe" : OperatingSystem.IsMacOS() ? "open" : "xdg-open";
            Process.Start(new ProcessStartInfo(opener) { ArgumentList = { folder }, UseShellExecute = false });
        }
        catch (Exception e) { Log.Warn($"Could not open folder: {e.Message}"); }
    }

    private static readonly object _writeLock = new();

    /// <summary>
    /// Write via a unique temp file + rename; keeps the previous file as .bak. Serialised so an autosave task and a
    /// sync save (exit, manual save) can never interleave on the same file.
    /// </summary>
    public static void WriteAtomic(string path, string text)
    {
        lock (_writeLock)
        {
            string tmp = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(tmp, text);
                if (File.Exists(path)) File.Copy(path, path + ".bak", overwrite: true);
                File.Move(tmp, path, overwrite: true);
            }
            finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
        }
    }

    /// <summary>Reads a JSON file, falling back to its .bak if the main copy is missing or corrupt.</summary>
    public static T? ReadJson<T>(string path) where T : class
    {
        foreach (string p in new[] { path, path + ".bak" })
        {
            if (!File.Exists(p)) continue;
            try { return JsonSerializer.Deserialize<T>(File.ReadAllText(p)); }
            catch (Exception e) { Log.Warn($"{Path.GetFileName(p)} unreadable ({e.Message}); trying backup."); }
        }
        return null;
    }

    // ---- Thumbnails --------------------------------------------------------------------------------------------

    private static readonly Dictionary<string, (DateTime Stamp, Texture2D? Tex)> _thumbs = [];

    /// <summary>Renders the camera into a 320x180 texture and writes icon.png (atomically).</summary>
    public static void CaptureIcon(Camera cam, string folder)
    {
        if (Application.IsHeadless) return;
        const int W = 320, H = 180;
        try
        {
            var rt = new RenderTexture(W, H, true, [TextureImageFormat.Color4b]);
            var prev = cam.Target;
            cam.Target = rt;
            cam.UpdateRenderData();
            (cam.Pipeline ?? DefaultRenderPipeline.Default).Render(cam, new RenderingData());
            cam.Target = prev;
            var px = new byte[W * H * 4];
            rt.MainTexture.GetData(new Memory<byte>(px));
            rt.Dispose();
            for (int i = 3; i < px.Length; i += 4) px[i] = 255;
            var settings = new ImageMagick.MagickReadSettings { Width = W, Height = H, Format = ImageMagick.MagickFormat.Rgba, Depth = 8 };
            using var img = new ImageMagick.MagickImage(px, settings);
            img.Flip();
            string path = Path.Combine(folder, "icon.png");
            img.Write(path + ".tmp", ImageMagick.MagickFormat.Png);
            File.Move(path + ".tmp", path, overwrite: true);
            _thumbs.Remove(path);
        }
        catch (Exception e) { Log.Warn($"Thumbnail capture failed: {e.Message}"); }
    }

    public static Texture2D? Thumbnail(WorldEntry w)
    {
        string p = w.Icon;
        if (!File.Exists(p)) return null;
        DateTime stamp = File.GetLastWriteTimeUtc(p);
        if (_thumbs.TryGetValue(p, out var t) && t.Stamp == stamp) return t.Tex;
        Texture2D? tex = null;
        try { using var fs = File.OpenRead(p); tex = Texture2D.FromStream(fs); } catch { }
        _thumbs[p] = (stamp, tex);
        return tex;
    }
}
