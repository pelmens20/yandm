using System.Runtime.InteropServices;

namespace YandexMusicController.Services;

/// <summary>
/// Detects whether the foreground window is roughly fullscreen (exclusive or borderless).
/// </summary>
public static class FullscreenDetect
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    public static bool IsForegroundFullscreen()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;

            // Our own windows are never "game fullscreen".
            var processPath = Environment.ProcessPath;
            // Compare sizes to the monitor the window is on.
            if (!GetWindowRect(hwnd, out var wr)) return false;

            var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            var mi = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref mi)) return false;

            var mw = mi.rcMonitor.Right - mi.rcMonitor.Left;
            var mh = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
            var ww = wr.Right - wr.Left;
            var wh = wr.Bottom - wr.Top;

            // Borderless fullscreen is usually exact match; allow 2px slack.
            return Math.Abs(ww - mw) <= 2 && Math.Abs(wh - mh) <= 2
                   && wr.Left <= mi.rcMonitor.Left + 2
                   && wr.Top <= mi.rcMonitor.Top + 2;
        }
        catch
        {
            return false;
        }
    }
}
