using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using GameScrapbook.Windows;
using GameScrapbook.Windows.Capture;
using GameScrapbook.Windows.Interop;

internal static class Program
{
    private static readonly List<object> Results = [];
    private static int _passed;
    private static int _failed;

    [STAThread]
    private static int Main(string[] args)
    {
        Run("capture snapshots and remembers game clipping before taking focus", () =>
        {
            using var f = new Fixture(); f.Begin();
            Check(f.C.IsCapturing && TestPlatform.Foreground == f.Selection.Handle, "overlay must own foreground");
            Before("clip-read", "selection-focus-attempt"); Before("snapshot", "selection-focus-attempt");
            Check(TestPlatform.Clip.SameAs(TestPlatform.Desktop), "cursor must be free across monitors");
            Check(f.Timer.IsEnabled, "polling should begin after focus acquired");
        });
        Run("denied activation aborts without starting gestures or returning cursor", () =>
        {
            using var f = new Fixture(); TestPlatform.ActivationAllowed = false; f.C.BeginCapture();
            Check(!f.C.IsCapturing && !f.Timer.IsEnabled, "denied activation cannot start session");
            Check(f.Errors.Count == 1 && TestPlatform.Overlays.All(x => x.Closed), "denial must report and close overlay");
            NoReturn(); Check(TestPlatform.Foreground == 101, "game focus must remain unchanged");
        });
        Run("successful selection and drag save once then return cursor before focus", () =>
        {
            using var f = new Fixture(); f.Begin(); f.SelectAndDrag(); f.Drop();
            Check(TestPlatform.Saved == 1 && !f.C.IsCapturing, "exactly one placed image");
            Before("cursor-return", "selection-close"); Before("cursor-return", "focus:101");
            Check(TestPlatform.ForegroundAtCursorReturn >= 200 && TestPlatform.Foreground == 101, "warp while overlay still owns input");
            Check(TestPlatform.Clip.SameAs(TestPlatform.Monitor), "original game clipping restored");
            Check(TestPlatform.Overlays.All(x => x.Closed), "all overlays closed");
        });
        Run("original game regaining focus cancels before a pending drop", () =>
        {
            using var f = new Fixture(); f.Begin(); f.SelectAndDrag(); TestPlatform.Events.Clear();
            TestPlatform.Foreground = 101; f.Tick(false, new Point(800, 100));
            Check(!f.C.IsCapturing && TestPlatform.Saved == 0, "game reactivation must abort drop");
            NoReturn(); Check(TestPlatform.Foreground == 101, "must not reacquire foreground");
        });
        Run("Alt+Tab to another app cancels without moving cursor or stealing focus", () =>
        {
            using var f = new Fixture(); f.Begin(); f.SelectAndDrag(); TestPlatform.Events.Clear();
            TestPlatform.Foreground = 102; f.Tick(false, new Point(800, 100));
            Check(!f.C.IsCapturing && TestPlatform.Saved == 0, "external switch aborts");
            NoReturn(); Check(TestPlatform.Foreground == 102, "other app remains foreground");
        });
        Run("switching to scrapbook itself cancels without restoring game", () =>
        {
            using var f = new Fixture(); f.Begin(); TestPlatform.Events.Clear(); TestPlatform.Foreground = 100; f.Tick(false);
            Check(!f.C.IsCapturing, "board switch aborts"); NoReturn();
            Check(TestPlatform.Foreground == 100, "board stays foreground");
        });
        Run("unknown foreground cancels without returning cursor", () =>
        {
            using var f = new Fixture(); f.Begin(); TestPlatform.Events.Clear(); TestPlatform.Foreground = 0; f.Tick(false);
            Check(!f.C.IsCapturing, "no foreground cannot continue capture"); NoReturn();
        });
        Run("outside-board drop retains the selected snip and overlay focus for retry", () =>
        {
            using var f = new Fixture(); f.Begin(); f.SelectAndDrag(); TestPlatform.ValidDrop = false; f.Drop();
            Check(f.C.IsCapturing && TestPlatform.Saved == 0 && TestPlatform.Foreground == f.Selection.Handle, "invalid drop remains ready");
            Check(TestPlatform.Overlays.Last().ClickThrough && TestPlatform.Overlays.Last().Closed, "ghost closes without taking focus");
            TestPlatform.ValidDrop = true; f.DragAgain(); f.Drop();
            Check(!f.C.IsCapturing && TestPlatform.Saved == 1, "retry saves once");
        });
        Run("failed save retains selected snip and focus then a retry succeeds", () =>
        {
            using var f = new Fixture(); f.Begin(); f.SelectAndDrag(); TestPlatform.SaveFails = true; f.Drop();
            Check(f.C.IsCapturing && TestPlatform.Saved == 0 && f.Errors.Count == 1, "failed save remains retryable");
            Check(TestPlatform.Foreground == f.Selection.Handle, "overlay retains input during retry");
            TestPlatform.SaveFails = false; f.DragAgain(); f.Drop(); Check(TestPlatform.Saved == 1 && !f.C.IsCapturing, "retry saves");
        });
        Run("ReturnCursor off leaves cursor at drop while returning game focus", () =>
        {
            using var f = new Fixture(); f.C.ReturnCursor = false; f.Begin(); f.SelectAndDrag(); TestPlatform.Events.Clear(); f.Drop();
            Check(!TestPlatform.Events.Contains("cursor-return"), "cursor option respected");
            Check(TestPlatform.Cursor == new Point(800, 100) && TestPlatform.Foreground == 101, "drop position and game focus");
        });
        Run("right-click cancellation returns cursor before closing overlay", () =>
        {
            using var f = new Fixture(); f.Begin(); TestPlatform.Keys.Add(2); f.Tick(false);
            Check(!f.C.IsCapturing && TestPlatform.Saved == 0, "right click cancels"); Before("cursor-return", "selection-close");
        });
        Run("Escape hotkey cancels without passing a normal key to the game", () =>
        {
            using var f = new Fixture(); f.Begin(); Check(HwndSource.Hotkey(0x4712), "Escape hook handled");
            Check(!f.C.IsCapturing && TestPlatform.Foreground == 101, "Escape returns game"); Before("cursor-return", "selection-close");
        });
        Run("Escape polling fallback still cancels when its registration is unavailable", () =>
        {
            using var f = new Fixture(); TestPlatform.EscapeRegistrationAllowed = false; f.Begin();
            TestPlatform.Keys.Add(0x1B); f.Tick(false); Check(!f.C.IsCapturing, "fallback cancels");
        });
        Run("closed game target is never focused and its old cursor clip is not restored", () =>
        {
            using var f = new Fixture(); f.Begin(); TestPlatform.Windows.Remove(101); TestPlatform.Events.Clear(); f.C.Cancel();
            Check(!f.C.IsCapturing, "cancel closes overlay even if game closed");
            Check(!TestPlatform.Events.Contains("focus:101") && !TestPlatform.Events.Contains("clip-restore"), "closed target remains untouched");
        });
        Run("reused source HWND from a different process is not restored", () =>
        {
            using var f = new Fixture(); f.Begin(); TestPlatform.Windows[101] = 99; TestPlatform.Events.Clear(); f.C.Cancel();
            Check(!TestPlatform.Events.Contains("focus:101") && !TestPlatform.Events.Contains("clip-restore"), "process identity protects target");
        });
        Run("focus return denial makes one attempt and does not confine another app", () =>
        {
            using var f = new Fixture(); f.Begin(); TestPlatform.RestoreAllowed = false; TestPlatform.Events.Clear(); f.C.Cancel();
            Check(TestPlatform.Events.Count(x => x == "focus:101") == 1, "one ordinary focus attempt");
            Check(!TestPlatform.Events.Contains("clip-restore") && !f.C.IsCapturing, "no confinement after focus refusal");
        });
        Run("another app's replacement cursor clip is preserved", () =>
        {
            using var f = new Fixture(); f.Begin(); var replacement = new NativeMethods.NativeRect { Left = 10, Top = 10, Right = 100, Bottom = 100 };
            TestPlatform.Clip = replacement; TestPlatform.Events.Clear(); f.C.Cancel();
            Check(TestPlatform.Clip.SameAs(replacement) && !TestPlatform.Events.Contains("clip-restore"), "never overwrite foreign clip");
        });
        Run("left-handed mouse mapping still selects and drops with the logical primary", () =>
        {
            using var f = new Fixture(); TestPlatform.Swapped = true; f.Begin(); f.SelectAndDrag(); f.Drop();
            Check(TestPlatform.Saved == 1 && !f.C.IsCapturing, "swapped primary gesture succeeds");
        });
        Run("Dispose during capture returns cursor before closing and restores focus", () =>
        {
            var f = new Fixture(); f.Begin(); TestPlatform.Events.Clear(); f.C.Dispose();
            Check(!f.C.IsCapturing && !f.Timer.IsEnabled, "disposal stops session");
            Before("cursor-return", "selection-close"); Check(TestPlatform.Foreground == 101, "disposal restores owned focus");
        });
        Run("Dispose after an external switch does not move cursor or steal focus", () =>
        {
            var f = new Fixture(); f.Begin(); TestPlatform.Events.Clear(); TestPlatform.Foreground = 102; f.C.Dispose(); NoReturn();
            Check(TestPlatform.Foreground == 102, "external app remains foreground on dispose");
        });
        Run("host capture gate prevents snapshot and focus changes", () =>
        {
            using var f = new Fixture(); f.C.CanBeginCapture = () => false; f.C.BeginCapture();
            Check(!f.C.IsCapturing && !TestPlatform.Events.Contains("snapshot") && !TestPlatform.Events.Contains("selection-focus-attempt"), "host gate respected");
        });
        Run("capture started in scrapbook returns to scrapbook without an external game target", () =>
        {
            using var f = new Fixture(); TestPlatform.Foreground = 100; TestPlatform.Clip = TestPlatform.Desktop;
            f.Begin(); TestPlatform.Events.Clear(); f.C.Cancel();
            Check(!f.C.IsCapturing && TestPlatform.Foreground == 100, "own-app capture restores owner");
            Before("cursor-return", "focus:100"); Before("cursor-return", "selection-close");
            Check(!TestPlatform.Events.Contains("clip-restore"), "no external game clip restored");
        });
        string reportPath = args.Length == 0 ? Path.Combine(AppContext.BaseDirectory, "results.json")
            : args.Length == 2 && args[0] == "--report" ? Path.GetFullPath(args[1])
            : throw new ArgumentException("Usage: GameScrapbook.Capture.Checks [--report path.json]");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            status = _failed == 0 ? "passed" : "failed", passed = _passed, failed = _failed,
            boundary = "Actual linked CaptureCoordinator source; simulated platform/window/snapshot operations. Unshown WPF owner and dispatcher only. No native HWND, real input, user board, game capture, or live Nivalis Nights test.",
            sourceSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(FindCoordinatorSource()))).ToLowerInvariant(),
            cases = Results
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"RESULT: {_passed} passed, {_failed} failed.");
        return _failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try { test(); _passed++; Results.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { _failed++; Results.Add(new { name, passed = false, error = ex.GetBaseException().Message }); Console.WriteLine("FAIL " + name + ": " + ex.GetBaseException().Message); }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static string FindCoordinatorSource()
    {
        const string relativePath = "src/GameScrapbook.Windows/Capture/CaptureCoordinator.cs";
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (DirectoryInfo? directory = new(start); directory is not null; directory = directory.Parent)
            {
                string path = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(path)) return path;
            }
        }
        throw new FileNotFoundException("Run these checks from the source checkout so its coordinator can be hashed.");
    }
    private static void Before(string first, string second)
    {
        int a = TestPlatform.Events.IndexOf(first), b = TestPlatform.Events.IndexOf(second);
        Check(a >= 0 && b > a, $"Expected {first} before {second}: " + string.Join(", ", TestPlatform.Events));
    }
    private static void NoReturn() => Check(!TestPlatform.Events.Any(x => x == "cursor-return" || x.StartsWith("focus:") || x == "clip-restore"), "unexpected focus/cursor/clip return");
    private sealed class Fixture : IDisposable
    {
        internal CaptureCoordinator C { get; }
        internal List<string> Errors { get; } = [];
        internal DispatcherTimer Timer => (DispatcherTimer)typeof(CaptureCoordinator).GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(C)!;
        internal OverlayWindow Selection => TestPlatform.Overlays.First(x => !x.ClickThrough);
        internal Fixture()
        {
            TestPlatform.Reset();
            var owner = new Window { WindowState = WindowState.Minimized };
            C = new CaptureCoordinator(owner,
                p => TestPlatform.ValidDrop && p.X >= 640 ? new CaptureDropTarget(new Point(p.X - 640, p.Y)) : null,
                (image, p) => { TestPlatform.Events.Add("drop-save"); if (TestPlatform.SaveFails) throw new IOException("simulated disk failure"); TestPlatform.Saved++; },
                _ => { });
            C.CaptureError += message => Errors.Add(message);
        }
        internal void Begin() { C.BeginCapture(); Check(C.IsCapturing, "session failed to start: " + string.Join("; ", Errors)); }
        internal void Tick(bool primary, Point? cursor = null)
        {
            int key = TestPlatform.Swapped ? 2 : 1;
            if (primary) TestPlatform.Keys.Add(key); else TestPlatform.Keys.Remove(key);
            if (cursor is Point p) TestPlatform.Cursor = p;
            typeof(CaptureCoordinator).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(C, [null, EventArgs.Empty]);
        }
        internal void SelectAndDrag()
        {
            Tick(true, new Point(100, 100)); Tick(true, new Point(220, 180)); Tick(false);
            DragAgain();
        }
        internal void DragAgain() => Tick(true, new Point(140, 140));
        internal void Drop() => Tick(false, new Point(800, 100));
        public void Dispose() => C.Dispose();
    }
}
