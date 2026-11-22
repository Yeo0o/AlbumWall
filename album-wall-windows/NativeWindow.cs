using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AlbumWall;

internal static class NativeWindow
{
    public const int ChildStyle = 0x40000000;
    public const int VisibleStyle = 0x10000000;
    public const int NoActivateStyle = 0x08000000;
    public const uint NoZOrder = 0x0004;
    public const uint NoActivate = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X, Y;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool MonitorCallback(nint monitor, nint context, ref Rect bounds, nint data);

    public static List<Rect> GetMonitorBounds()
    {
        var monitors = new List<Rect>();
        EnumDisplayMonitors(0, 0, (nint monitor, nint context, ref Rect bounds, nint data) =>
        {
            monitors.Add(bounds);
            return true;
        }, 0);
        return monitors;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint context, nint clipRect, MonitorCallback callback, nint data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(nint window, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessDpiAwarenessContext(nint value);
}
