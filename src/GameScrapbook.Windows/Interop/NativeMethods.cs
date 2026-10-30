using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace GameScrapbook.Windows.Interop;

internal static class NativeMethods
{
    internal const int WmHotkey = 0x0312;
    internal const int WmMouseActivate = 0x0021;
    internal const int WmNcHitTest = 0x0084;
    internal const int MaNoActivate = 3;
    internal const int HtTransparent = -1;
    internal const int GwlExStyle = -20;
    internal const long WsExNoActivate = 0x08000000L;
    internal const long WsExToolWindow = 0x00000080L;
    internal const long WsExTransparent = 0x00000020L;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpShowWindow = 0x0040;
    internal const uint ModNoRepeat = 0x4000;
    internal static readonly nint HwndTopmost = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint
    {
        public int X;
        public int Y;
        public readonly Point ToPoint() => new(X, Y);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
        public readonly Rect ToRect() => new(Left, Top, Width, Height);
        public readonly bool SameAs(NativeRect other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    internal static NativeRect MonitorBounds(nint window)
    {
        var monitor = MonitorFromWindow(window, 2); // MONITOR_DEFAULTTONEAREST.
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot find the game's display.");
        return info.Monitor;
    }

    /// <summary>The screen to snip is determined by the physical pointer, not window focus.</summary>
    internal static NativeRect MonitorBoundsAt(Point physicalPoint)
    {
        var point = new NativePoint { X = (int)physicalPoint.X, Y = (int)physicalPoint.Y };
        var monitor = MonitorFromPoint(point, 2); // MONITOR_DEFAULTTONEAREST, including negative origins.
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot find the display under the mouse pointer.");
        return info.Monitor;
    }

    /// <summary>Resolve only a visible external window; never choose an unrelated historical window.</summary>
    internal static nint FocusReturnTarget(nint foreground, Point physicalPoint)
    {
        if (IsExternalFocusCandidate(foreground)) return foreground;
        var point = new NativePoint { X = (int)physicalPoint.X, Y = (int)physicalPoint.Y };
        nint underPointer = WindowFromPoint(point);
        nint root = underPointer == 0 ? 0 : GetAncestor(underPointer, 2); // GA_ROOT.
        return IsExternalFocusCandidate(root) ? root : 0;
    }

    private static bool IsExternalFocusCandidate(nint window)
    {
        if (window == 0 || !IsWindow(window) || !IsWindowVisible(window) || IsIconic(window)
            || window == GetDesktopWindow() || window == GetShellWindow()) return false;
        GetWindowThreadProcessId(window, out uint process);
        return process != 0 && process != (uint)Environment.ProcessId;
    }

    internal static string DescribeWindowBounds(nint window)
    {
        return GetWindowRect(window, out var bounds)
            ? $"{bounds.Left},{bounds.Top} {bounds.Width}x{bounds.Height}"
            : "unavailable";
    }

    internal static bool TryWindowBounds(nint window, out NativeRect bounds) => GetWindowRect(window, out bounds);

    internal static bool RestoreWithoutActivation(nint window)
    {
        _ = ShowWindow(window, 4); // SW_SHOWNOACTIVATE: restore last size/position without activating.
        return IsWindowVisible(window) && !IsIconic(window);
    }

    internal static NativeRect VirtualDesktopBounds()
    {
        int x = GetSystemMetrics(76), y = GetSystemMetrics(77);
        return new NativeRect { Left = x, Top = y, Right = x + GetSystemMetrics(78), Bottom = y + GetSystemMetrics(79) };
    }

    internal static Point PhysicalCursor()
    {
        if (!GetPhysicalCursorPos(out var point))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot read the mouse position.");
        return point.ToPoint();
    }

    internal static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    internal static void ConfigureOverlay(nint hwnd, bool clickThrough)
    {
        long style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        style |= WsExToolWindow;
        if (clickThrough) style |= WsExNoActivate | WsExTransparent;
        else style &= ~(WsExNoActivate | WsExTransparent);
        SetWindowLongPtr(hwnd, GwlExStyle, new nint(style));
        // WindowFromPoint explicitly skips disabled windows; the host's drop hit-test sees the board.
        if (clickThrough) EnableWindow(hwnd, false);
    }

    internal static void PlacePhysical(nint hwnd, int x, int y, int width, int height)
    {
        if (!SetWindowPos(hwnd, HwndTopmost, x, y, Math.Max(1, width), Math.Max(1, height), SwpNoActivate | SwpShowWindow))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot position the capture overlay.");
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetPhysicalCursorPos(out NativePoint point);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetPhysicalCursorPos(int x, int y);
    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();
    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect bounds);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClipCursor(out NativeRect bounds);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClipCursor(nint bounds);
    [DllImport("user32.dll", EntryPoint = "ClipCursor", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RestoreClipCursor(ref NativeRect bounds);
    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool enable);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")]
    internal static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool BitBlt(nint destination, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(nint dc);
}
