namespace Sandbox;

/// <summary>Where saves, settings, logs and generated caches live. Standalone: ~/.config/ProwlSandbox (ApplicationData).
/// Inside the editor, play tests use the project's Library/SandboxData so they never touch the player's real saves.
/// SANDBOX_DATA_DIR overrides both.</summary>
public static class DataPaths
{
    public static string Root
    {
        get
        {
            if (Environment.GetEnvironmentVariable("SANDBOX_DATA_DIR") is { Length: > 0 } env) return env;
#if PROWL_EDITOR
            if (Prowl.Editor.Projects.Project.Current is { } p) return Path.Combine(p.RootPath, "Library", "SandboxData");
#endif
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProwlSandbox");
        }
    }
}
