using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameScrapbook.Windows
{
    internal static class App
    {
        internal static void WriteCaptureDiagnostic(string message) => TestPlatform.Diagnostics.Add(message);
    }

    internal static class TestPlatform
    {
        internal static readonly List<string> Events = [];
        internal static readonly List<string> Diagnostics = [];
        internal static readonly List<Capture.OverlayWindow> Overlays = [];
        internal static bool ActivationAllowed = true;
        internal static bool SaveFails;
        internal static bool ValidDrop = true;
        internal static bool RestoreAllowed = true;
        internal static bool EscapeRegistrationAllowed = true;
        internal static bool UnclipOnActivation = true;
        internal static int Saved;
        internal static int NextHandle = 200;
        internal static nint Foreground = 101;
        internal static nint ForegroundAtCursorReturn;
        internal static Point Cursor = new(100, 100);
        internal static readonly HashSet<int> Keys = [];
        internal static readonly Dictionary<nint, uint> Windows = [];
        internal static readonly HashSet<nint> Minimized = [];
        internal static Interop.NativeMethods.NativeRect Desktop = new() { Left = 0, Top = 0, Right = 1280, Bottom = 480 };
        internal static Interop.NativeMethods.NativeRect Monitor = new() { Left = 0, Top = 0, Right = 640, Bottom = 480 };
        internal static Interop.NativeMethods.NativeRect Clip;
        internal static bool Swapped;

        internal static void Reset()
        {
            Events.Clear(); Diagnostics.Clear(); Overlays.Clear(); Keys.Clear(); Windows.Clear(); Minimized.Clear();
            Windows[100] = (uint)Environment.ProcessId; Windows[101] = 42; Windows[102] = 43;
            Minimized.Add(100);
            ActivationAllowed = RestoreAllowed = EscapeRegistrationAllowed = UnclipOnActivation = ValidDrop = true;
            SaveFails = Swapped = false; Saved = 0; NextHandle = 200;
            Foreground = 101; ForegroundAtCursorReturn = 0; Cursor = new Point(100, 100);
            Desktop = new() { Left = 0, Top = 0, Right = 1280, Bottom = 480 };
            Monitor = new() { Left = 0, Top = 0, Right = 640, Bottom = 480 };
            Clip = Monitor;
            Capture.HwndSource.Reset();
        }

        internal static BitmapSource Image(int width = 640, int height = 480)
        {
            var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null,
                new byte[width * height * 4], width * 4);
            image.Freeze();
            return image;
        }
    }
}

namespace GameScrapbook.Windows.Capture
{
    // Lexical namespace shims let the actual coordinator constructor run without a native HWND.
    internal sealed class WindowInteropHelper(Window owner)
    {
        internal nint EnsureHandle() { owner.Dispatcher.VerifyAccess(); return 100; }
    }

    internal sealed class HwndSource
    {
        private static readonly HwndSource Instance = new();
        private readonly List<HwndSourceHook> _hooks = [];
        internal static HwndSource? FromHwnd(nint hwnd) => Instance;
        internal static void Reset() => Instance._hooks.Clear();
        internal void AddHook(HwndSourceHook hook) => _hooks.Add(hook);
        internal void RemoveHook(HwndSourceHook hook) => _hooks.Remove(hook);
        internal static bool Hotkey(int id)
        {
            bool handled = false;
            foreach (var hook in Instance._hooks.ToArray()) hook(100, 0x0312, id, 0, ref handled);
            return handled;
        }
    }

    internal class OverlayWindow
    {
        private readonly bool _clickThrough;
        internal nint Handle { get; private set; }
        internal bool Closed { get; private set; }
        internal bool ClickThrough => _clickThrough;
        internal OverlayWindow(UIElement content, bool clickThrough)
        {
            _clickThrough = clickThrough;
            Handle = TestPlatform.NextHandle++;
            TestPlatform.Windows[Handle] = (uint)Environment.ProcessId;
            TestPlatform.Overlays.Add(this);
        }
        internal void Present(int x, int y, int width, int height) => TestPlatform.Events.Add(_clickThrough ? "ghost-present" : "selection-present");
        internal void MovePhysical(int x, int y, int width, int height) => TestPlatform.Events.Add("ghost-move");
        internal string DescribeGeometry() => "synthetic geometry";
        internal bool TakeInputFocus()
        {
            TestPlatform.Events.Add("selection-focus-attempt");
            if (_clickThrough || !TestPlatform.ActivationAllowed) return false;
            TestPlatform.Foreground = Handle;
            if (TestPlatform.UnclipOnActivation) TestPlatform.Clip = TestPlatform.Desktop;
            return true;
        }
        internal void Close()
        {
            TestPlatform.Events.Add(_clickThrough ? "ghost-close" : "selection-close");
            Closed = true;
            TestPlatform.Windows.Remove(Handle);
            if (TestPlatform.Foreground == Handle) TestPlatform.Foreground = 0;
            Handle = 0;
        }
    }

    internal sealed class SelectionSurface(BitmapSource snapshot) : FrameworkElement
    {
        internal BitmapSource Snapshot { get; } = snapshot;
        internal void SetSelection(Rect selection, bool ready = false, bool dragging = false) { }
    }
    internal sealed class DragSurface(BitmapSource image) : FrameworkElement
    {
        internal BitmapSource Image { get; } = image;
        internal bool ValidTarget { get; set; }
    }
    internal static class ScreenCapture
    {
        internal static BitmapSource Snapshot(Interop.NativeMethods.NativeRect monitor)
        {
            TestPlatform.Events.Add("snapshot");
            return TestPlatform.Image(monitor.Width, monitor.Height);
        }
    }
}

namespace GameScrapbook.Windows.Interop
{
    internal static class NativeMethods
    {
        internal const int WmHotkey = 0x0312;
        internal const uint ModNoRepeat = 0x4000;
        internal struct NativeRect
        {
            public int Left, Top, Right, Bottom;
            public readonly int Width => Right - Left;
            public readonly int Height => Bottom - Top;
            public readonly Rect ToRect() => new(Left, Top, Width, Height);
            public readonly bool SameAs(NativeRect other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
        }
        internal static Point PhysicalCursor() => TestPlatform.Cursor;
        internal static nint GetForegroundWindow() => TestPlatform.Foreground;
        internal static nint FocusReturnTarget(nint foreground, Point cursor) => foreground == 100 ? 0 : foreground;
        internal static uint GetWindowThreadProcessId(nint hwnd, out uint process)
        {
            process = TestPlatform.Windows.GetValueOrDefault(hwnd);
            return process == 0 ? 0u : 1u;
        }
        internal static NativeRect MonitorBoundsAt(Point point) => TestPlatform.Monitor;
        internal static NativeRect VirtualDesktopBounds() => TestPlatform.Desktop;
        internal static bool IsIconic(nint hwnd) => TestPlatform.Minimized.Contains(hwnd);
        internal static bool IsWindow(nint hwnd) => TestPlatform.Windows.ContainsKey(hwnd);
        internal static bool RestoreWithoutActivation(nint hwnd)
        {
            TestPlatform.Events.Add("owner-restore-passive"); TestPlatform.Minimized.Remove(hwnd); return true;
        }
        internal static bool GetClipCursor(out NativeRect clip)
        {
            TestPlatform.Events.Add("clip-read"); clip = TestPlatform.Clip; return true;
        }
        internal static bool ClipCursor(nint value)
        {
            TestPlatform.Events.Add("clip-release"); TestPlatform.Clip = TestPlatform.Desktop; return true;
        }
        internal static bool RestoreClipCursor(ref NativeRect clip)
        {
            TestPlatform.Events.Add("clip-restore"); TestPlatform.Clip = clip; return true;
        }
        internal static int GetSystemMetrics(int index) => index == 23 && TestPlatform.Swapped ? 1 : 0;
        internal static bool IsDown(int key) => TestPlatform.Keys.Contains(key);
        internal static bool RegisterHotKey(nint hwnd, int id, uint mods, uint key)
        {
            TestPlatform.Events.Add($"hotkey-register:{id}");
            return key != 0x1B || TestPlatform.EscapeRegistrationAllowed;
        }
        internal static bool UnregisterHotKey(nint hwnd, int id)
        {
            TestPlatform.Events.Add($"hotkey-unregister:{id}"); return true;
        }
        internal static bool SetForegroundWindow(nint hwnd)
        {
            TestPlatform.Events.Add($"focus:{hwnd}");
            if (!TestPlatform.RestoreAllowed) return false;
            TestPlatform.Foreground = hwnd;
            return true;
        }
        internal static bool SetPhysicalCursorPos(int x, int y)
        {
            TestPlatform.Events.Add("cursor-return");
            TestPlatform.ForegroundAtCursorReturn = TestPlatform.Foreground;
            TestPlatform.Cursor = new Point(x, y);
            return true;
        }
    }
}
