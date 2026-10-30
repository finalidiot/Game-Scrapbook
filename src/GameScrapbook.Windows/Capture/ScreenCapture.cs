using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using GameScrapbook.Windows.Interop;

namespace GameScrapbook.Windows.Capture;

/// <summary>One-shot desktop capture. Windowed/borderless SDR is the initial compatibility target.</summary>
internal static class ScreenCapture
{
    internal static BitmapSource Snapshot(NativeMethods.NativeRect bounds)
    {
        if (bounds.Width < 1 || bounds.Height < 1 || (long)bounds.Width * bounds.Height > 100_000_000)
            throw new InvalidOperationException("The selected display has an unsupported size.");

        nint screen = 0, memory = 0, bitmap = 0, previous = 0;
        try
        {
            screen = NativeMethods.GetDC(0);
            if (screen == 0) throw Failure("Cannot access this display.");
            memory = NativeMethods.CreateCompatibleDC(screen);
            if (memory == 0) throw Failure("Cannot create the screenshot buffer.");
            bitmap = NativeMethods.CreateCompatibleBitmap(screen, bounds.Width, bounds.Height);
            if (bitmap == 0) throw Failure("Cannot allocate the screenshot image.");
            previous = NativeMethods.SelectObject(memory, bitmap);
            if (previous == 0 || previous == new nint(-1)) throw Failure("Cannot prepare the screenshot image.");
            // CAPTUREBLT also includes visible layered desktop windows. This happens BEFORE our overlay exists.
            if (!NativeMethods.BitBlt(memory, 0, 0, bounds.Width, bounds.Height, screen, bounds.Left, bounds.Top, 0x00CC0020 | 0x40000000))
                throw Failure("Windows could not capture this display. Try borderless or windowed mode.");
            var result = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            result.Freeze();
            return result;
        }
        finally
        {
            if (memory != 0 && previous != 0 && previous != new nint(-1)) NativeMethods.SelectObject(memory, previous);
            if (bitmap != 0) NativeMethods.DeleteObject(bitmap);
            if (memory != 0) NativeMethods.DeleteDC(memory);
            if (screen != 0) NativeMethods.ReleaseDC(0, screen);
        }
    }

    private static Win32Exception Failure(string message) => new(Marshal.GetLastWin32Error(), message);
}
