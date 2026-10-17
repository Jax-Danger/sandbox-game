using Prowl.Runtime;

namespace Sandbox;

internal static class Program
{
    private static int Main(string[] args)
    {
        bool headless = args.Any(static a => string.Equals(a, "--headless", StringComparison.OrdinalIgnoreCase));
        if (!headless) Log.Init();
        var game = new SandboxGame();
        if (headless)
        {
            int si = Array.IndexOf(args, "--soak");
            float soakSecs = si >= 0 && si + 1 < args.Length && float.TryParse(args[si + 1], out float ss) ? ss : 120f;
            int seed = si >= 0 && si + 2 < args.Length && int.TryParse(args[si + 2], out int sd) ? sd : 1;
            game.SelfTest = si >= 0 ? new SelfTest { Soak = new Soak(soakSecs, seed) } : new SelfTest();
            game.RunHeadless(new HeadlessRunOptions { MaxSeconds = si >= 0 ? soakSecs + 20f : 26, TargetFps = 60 });
            return game.SelfTest.ExitCode;
        }

        int shot = Array.IndexOf(args, "--shot");
        if (shot >= 0 && shot + 1 < args.Length) game.ShotState = args[shot + 1];

        try
        {
            Settings s = Settings.Load();
            // SANDBOX_RES=1920x1080 overrides the window size (screenshots / testing).
            if (Environment.GetEnvironmentVariable("SANDBOX_RES") is { } res && res.Split('x') is [var rw, var rh]
                && int.TryParse(rw, out int iw) && int.TryParse(rh, out int ih)) { s.ResolutionWidth = iw; s.ResolutionHeight = ih; }
            game.Run("Sandbox", s.ResolutionWidth, s.ResolutionHeight);
            game.SaveOnExit();
            return 0;
        }
        catch (Exception e)
        {
            string? report = Log.Crash(e, "fatal");
            if (Log.IsGraphicsInit(e))
                Log.ErrorDialog("Prowl Sandbox: graphics not supported",
                    "Could not create an OpenGL 4.1 window. Please update your graphics driver (OpenGL 4.1 or newer is required; " +
                    "on Linux install Mesa 21+ or your GPU vendor's driver).\n\n" + e.GetBaseException().Message +
                    (report is null ? "" : $"\n\nDetails: {report}"));
            else
                Log.ErrorDialog("Prowl Sandbox crashed", e.GetBaseException().Message + (report is null ? "" : $"\n\nA crash report was written to:\n{report}"));
            return 1;
        }
    }
}
