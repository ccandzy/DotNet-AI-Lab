using System.Runtime.InteropServices;

namespace AiChatClient;

internal static class WindowWorkAreaManager
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmSettingChange = 0x001A;
    private const int WmDisplayChange = 0x007E;
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    public static IntPtr WindowProc(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message is WmSettingChange or WmDisplayChange)
        {
            ResizeMaximizedWindowToWorkArea(hwnd);
            return IntPtr.Zero;
        }

        if (message != WmGetMinMaxInfo || lParam == IntPtr.Zero) return IntPtr.Zero;

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return IntPtr.Zero;

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo)) return IntPtr.Zero;

        var bounds = CalculateMaximizedBounds(
            FromNativeRect(monitorInfo.Monitor),
            FromNativeRect(monitorInfo.WorkArea));
        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        minMaxInfo.MaxPosition = new NativePoint(bounds.Left, bounds.Top);
        minMaxInfo.MaxSize = new NativePoint(bounds.Width, bounds.Height);
        Marshal.StructureToPtr(minMaxInfo, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    internal static MaximizedBounds CalculateMaximizedBounds(ScreenRect monitor, ScreenRect workArea) =>
        new(
            workArea.Left - monitor.Left,
            workArea.Top - monitor.Top,
            workArea.Right - workArea.Left,
            workArea.Bottom - workArea.Top);

    private static ScreenRect FromNativeRect(NativeRect rect) =>
        new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    private static void ResizeMaximizedWindowToWorkArea(IntPtr hwnd)
    {
        if (!IsZoomed(hwnd)) return;
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return;
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo)) return;

        var work = monitorInfo.WorkArea;
        SetWindowPos(
            hwnd,
            IntPtr.Zero,
            work.Left,
            work.Top,
            work.Right - work.Left,
            work.Bottom - work.Top,
            SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public NativePoint(int x, int y) { X = x; Y = y; }
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    internal readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom);

    internal readonly record struct MaximizedBounds(int Left, int Top, int Width, int Height);
}
