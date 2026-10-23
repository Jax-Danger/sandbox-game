using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Sandbox;

/// <summary>
/// Tees all console output into &lt;AppData&gt;/ProwlSandbox/logs/latest.log (previous run kept as previous.log),
/// writes crash reports for unhandled exceptions, and shows a native error dialog for fatal start-up problems
/// (e.g. no OpenGL 4.1 driver).
/// </summary>
public static class Log
{
    public static string Dir { get; } = Path.Combine(DataPaths.Root, "logs");
    public static string? FilePath { get; private set; }
    private static StreamWriter? _file;

    public static void Init()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            FilePath = Path.Combine(Dir, "latest.log");
            if (File.Exists(FilePath)) File.Copy(FilePath, Path.Combine(Dir, "previous.log"), true);
            _file = new StreamWriter(FilePath, false, Encoding.UTF8) { AutoFlush = true };
            Console.SetOut(new Tee(Console.Out, _file));
            Console.SetError(new Tee(Console.Error, _file));
            Console.WriteLine($"Prowl Sandbox {Worlds.GameVersion} on {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture}), .NET {Environment.Version}, started {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception e) { Console.WriteLine($"Log file unavailable: {e.Message}"); }

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Crash(e.ExceptionObject as Exception, "unhandled exception");
        TaskScheduler.UnobservedTaskException += (_, e) => { Warn($"Background task failed: {e.Exception.GetBaseException().Message}"); e.SetObserved(); };
    }

    public static void Info(string msg) => Console.WriteLine(msg);
    public static void Warn(string msg) => Console.WriteLine("WARN " + msg);

    /// <summary>Writes crash-&lt;time&gt;.txt and returns its path.</summary>
    public static string? Crash(Exception? e, string what)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string path = Path.Combine(Dir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path,
                $"Prowl Sandbox {Worlds.GameVersion} crashed ({what}) at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                $"OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}\n.NET: {Environment.Version}\n\n{e}\n");
            Console.WriteLine($"CRASH ({what}): {e?.Message}. Report written to {path}");
            return path;
        }
        catch { return null; }
    }

    /// <summary>True when the exception looks like a missing/old OpenGL driver or window-system failure.</summary>
    public static bool IsGraphicsInit(Exception e)
    {
        string m = e.ToString();
        return m.Contains("GLFW", StringComparison.OrdinalIgnoreCase) || m.Contains("OpenGL", StringComparison.OrdinalIgnoreCase)
            || m.Contains("GL context", StringComparison.OrdinalIgnoreCase) || m.Contains("VersionUnavailable", StringComparison.OrdinalIgnoreCase)
            || m.Contains("WGL", StringComparison.Ordinal) || m.Contains("GLX", StringComparison.Ordinal);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);

    /// <summary>Best-effort native error dialog; always also logged.</summary>
    public static void ErrorDialog(string title, string message)
    {
        Console.WriteLine($"ERROR {title}: {message}");
        try
        {
            if (OperatingSystem.IsWindows()) { MessageBoxW(0, message, title, 0x10); return; }
            if (OperatingSystem.IsMacOS())
            {
                string esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
                Process.Start("osascript", ["-e", $"display alert \"{esc(title)}\" message \"{esc(message)}\" as critical"])?.WaitForExit(60000);
                return;
            }
            foreach (var (exe, args) in new (string, string[])[] { ("zenity", ["--error", "--title", title, "--text", message]), ("kdialog", ["--title", title, "--error", message]), ("xmessage", ["-center", $"{title}\n\n{message}"]) })
                try { Process.Start(exe, args)?.WaitForExit(60000); return; } catch { }
        }
        catch { }
    }

    private sealed class Tee(TextWriter a, TextWriter b) : TextWriter
    {
        public override Encoding Encoding => a.Encoding;
        public override void Write(char value) { a.Write(value); lock (b) b.Write(value); }
        public override void Write(string? value) { a.Write(value); lock (b) b.Write(value); }
        public override void WriteLine(string? value) { a.WriteLine(value); lock (b) b.WriteLine(value); }
        public override void Flush() { a.Flush(); lock (b) b.Flush(); }
    }
}
