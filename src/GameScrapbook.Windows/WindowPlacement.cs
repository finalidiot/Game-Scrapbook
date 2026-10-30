using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using GameScrapbook.Core;

namespace GameScrapbook.Windows;

internal static class WindowPlacement
{
    private const uint MonitorPrimary = 1;
    private const uint NoZOrder = 0x0004, NoActivate = 0x0010;

    public static void Restore(Window window, BoardSettings settings)
    {
        var monitors = GetMonitors();
        if (monitors.Count == 0) return;
        NativeRect wanted;
        if (settings.WindowLeft is double left && settings.WindowTop is double top)
            wanted = new NativeRect((int)left, (int)top, (int)(left + settings.WindowWidth), (int)(top + settings.WindowHeight));
        else
        {
            var target = monitors.FirstOrDefault(m => (m.Flags & MonitorPrimary) == 0);
            if (target.Work.Right == 0 && target.Work.Left == 0) target = monitors[0];
            int width = Math.Min(1040, target.Work.Right - target.Work.Left - 48);
            int height = Math.Min(780, target.Work.Bottom - target.Work.Top - 48);
            wanted = new NativeRect(target.Work.Left + 24, target.Work.Top + 24, target.Work.Left + 24 + width, target.Work.Top + 24 + height);
        }
        var best = monitors.OrderByDescending(m => IntersectionArea(m.Work, wanted)).First();
        int w = Math.Clamp(wanted.Right - wanted.Left, 500, Math.Max(500, best.Work.Right - best.Work.Left));
        int h = Math.Clamp(wanted.Bottom - wanted.Top, 350, Math.Max(350, best.Work.Bottom - best.Work.Top));
        int x = Math.Clamp(wanted.Left, best.Work.Left, Math.Max(best.Work.Left, best.Work.Right - w));
        int y = Math.Clamp(wanted.Top, best.Work.Top, Math.Max(best.Work.Top, best.Work.Bottom - h));
        SetWindowPos(new WindowInteropHelper(window).Handle, IntPtr.Zero, x, y, w, h, NoZOrder | NoActivate);
        if (settings.IsMaximized) window.WindowState = WindowState.Maximized;
    }

    public static void Remember(Window window, BoardSettings settings)
    {
        if (window.WindowState == WindowState.Minimized) return;
        settings.IsMaximized = window.WindowState == WindowState.Maximized;
        if (window.WindowState == WindowState.Normal && GetWindowRect(new WindowInteropHelper(window).Handle, out NativeRect rect))
        {
            settings.WindowLeft = rect.Left;
            settings.WindowTop = rect.Top;
            settings.WindowWidth = rect.Right - rect.Left;
            settings.WindowHeight = rect.Bottom - rect.Top;
        }
    }

    public static bool IsUnobscuredAt(Window window, Point p)
    {
        var target = new WindowInteropHelper(window).Handle;
        var current = GetTopWindow(IntPtr.Zero);
        for (int attempts = 0; current != IntPtr.Zero && attempts < 1024; attempts++, current = GetWindow(current, 2))
        {
            if (!IsWindowVisible(current) || IsIconic(current) || !GetWindowRect(current, out NativeRect rect) ||
                p.X < rect.Left || p.X >= rect.Right || p.Y < rect.Top || p.Y >= rect.Bottom) continue;
            if (current == target) return true;
            GetWindowThreadProcessId(current, out uint process);
            if (process == Environment.ProcessId) continue; // Our capture overlay, drag ghost, or tooltip.
            if ((GetWindowLongPtr(current, -20).ToInt64() & 0x20) != 0) continue; // Click-through overlay.
            return false;
        }
        return false;
    }

    private static List<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        MonitorEnum callback = (IntPtr monitor, IntPtr dc, ref NativeRect rect, IntPtr data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info)) list.Add(info);
            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return list;
    }

    private static long IntersectionArea(NativeRect a, NativeRect b) =>
        (long)Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left)) * Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect(int left, int top, int right, int bottom)
    { public int Left = left, Top = top, Right = right, Bottom = bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo
    { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    private delegate bool MonitorEnum(IntPtr monitor, IntPtr dc, ref NativeRect rect, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnum callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetTopWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
}
