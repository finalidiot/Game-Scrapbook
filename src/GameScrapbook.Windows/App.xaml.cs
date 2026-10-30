using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace GameScrapbook.Windows;

public partial class App : Application
{
    private Mutex? _instance;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        WriteCaptureDiagnostic("App starting, version 0.1.8.");
        _instance = new Mutex(true, @"Local\GameScrapbook-0.1", out _ownsMutex);
        if (!_ownsMutex)
        {
            WriteCaptureDiagnostic("Another instance is already running; this executable will exit.");
            var existing = FindWindow(null, "Game Scrapbook");
            if (existing != IntPtr.Zero)
            {
                if (IsIconic(existing)) ShowWindow(existing, 9);
                SetForegroundWindow(existing);
            }
            MessageBox.Show("Game Scrapbook is already running.\n\nIf you are opening an updated version, close the existing Game Scrapbook window first, then open this executable again.",
                "Game Scrapbook already open", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            WriteDiagnostic(args.Exception);
            (MainWindow as MainWindow)?.StopCaptureAfterError();
            MainWindow?.Hide();
            MessageBox.Show("Game Scrapbook encountered an unexpected error. Your last successfully saved board is still on disk.\n\n" +
                args.Exception.Message + "\n\nDetails: %LOCALAPPDATA%\\GameScrapbook\\diagnostics.log",
                "Game Scrapbook", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(1);
        };

        try
        {
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            WriteDiagnostic(ex);
            MessageBox.Show("Game Scrapbook could not start.\n\n" + ex.Message,
                "Game Scrapbook", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    public static void WriteDiagnostic(Exception ex)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameScrapbook");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "diagnostics.log");
            if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {ex}\n\n");
        }
        catch { /* A logging failure must not replace the original error. */ }
    }

    public static void WriteCaptureDiagnostic(string message)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameScrapbook");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "capture-diagnostics.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
                File.Move(path, path + ".previous", true);
            // Lifecycle/geometry/error information only: never screen contents,
            // window titles, or an ongoing stream of keyboard input.
            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {message.Replace('\r', ' ').Replace('\n', ' ')}\n");
        }
        catch { /* Diagnostic logging cannot prevent capture or startup. */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex) _instance?.ReleaseMutex();
        _instance?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string? windowName);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
}
