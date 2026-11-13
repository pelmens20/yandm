using System.Media;
using System.Runtime.InteropServices;
using System.Windows;

namespace YandexMusicController.Services;

public static class TrackAlert
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    /// <summary>
    /// Preferred top-right corner for the toast.
    /// If a game is focused on one monitor, place the toast on another monitor when available.
    /// </summary>
    public static RectD GetToastPlacement()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screens.Length == 0)
        {
            var w = SystemParameters.WorkArea;
            return new RectD(w.Right - 360, w.Top + 16, 340, 90);
        }

        System.Drawing.Rectangle? gameBounds = null;
        var fg = GetForegroundWindow();
        if (fg != IntPtr.Zero)
        {
            var hmon = MonitorFromWindow(fg, MonitorDefaultToNearest);
            var mi = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(hmon, ref mi))
            {
                gameBounds = System.Drawing.Rectangle.FromLTRB(
                    mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom);
            }
        }

        System.Windows.Forms.Screen? target = null;
        if (screens.Length > 1 && gameBounds is System.Drawing.Rectangle gb)
        {
            foreach (var s in screens)
            {
                var b = s.Bounds;
                var same =
                    Math.Abs(b.Left - gb.Left) <= 2 &&
                    Math.Abs(b.Top - gb.Top) <= 2 &&
                    Math.Abs(b.Width - gb.Width) <= 2 &&
                    Math.Abs(b.Height - gb.Height) <= 2;
                if (!same)
                {
                    target = s;
                    break;
                }
            }
        }

        target ??= System.Windows.Forms.Screen.PrimaryScreen ?? screens[0];
        var wa = target.WorkingArea;
        return new RectD(wa.Right - 360, wa.Top + 16, 340, 90);
    }

    public static void PlayCue()
    {
        try { SystemSounds.Asterisk.Play(); } catch { }
    }

    public readonly record struct RectD(double Left, double Top, double Width, double Height);
}
