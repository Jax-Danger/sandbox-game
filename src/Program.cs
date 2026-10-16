using Prowl.Runtime;

namespace Sandbox;

internal static class Program
{
    private static int Main(string[] args)
    {
        var game = new SandboxGame();
        if (args.Any(static a => string.Equals(a, "--headless", StringComparison.OrdinalIgnoreCase)))
        {
            game.SelfTest = new SelfTest();
            game.RunHeadless(new HeadlessRunOptions { MaxSeconds = 6, TargetFps = 60 });
            return game.SelfTest.ExitCode;
        }

        game.Run("Sandbox", 1280, 720);
        return 0;
    }
}
