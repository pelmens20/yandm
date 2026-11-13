using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace YandexMusicController;

/// <summary>
/// Always-on-top toast that does not steal focus.
/// Re-asserts HWND_TOPMOST while visible so borderless games are less likely to cover it.
/// Note: exclusive fullscreen (DirectX exclusive) still cannot show any external overlay.
/// </summary>
public partial class ToastWindow : Window
{
    private static ToastWindow? _current;
    private readonly DispatcherTimer _hideTimer;
    private readonly DispatcherTimer _topmostTimer;

    private const int GwlExstyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;

    private const int SwpNoActivate = 0x0010;
    private const int SwpNoMove = 0x0002;
    private const int SwpNoSize = 0x0001;
    private const int SwpShowWindow = 0x0040;
    private const int SwpNoOwnerZOrder = 0x0200;
    private const int SwShowNoActivate = 4;

    private static readonly IntPtr HwndTopMost = new(-1);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public ToastWindow()
    {
        InitializeComponent();
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        Topmost = true;

        SourceInitialized += (_, _) => ApplyOverlayStyles();

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _hideTimer.Tick += async (_, _) =>
        {
            _hideTimer.Stop();
            _topmostTimer.Stop();
            await FadeOutAndHideAsync();
        };

        // Keep forcing topmost while visible (helps against borderless games).
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _topmostTimer.Tick += (_, _) => ForceTopmost();
    }

    private IntPtr Handle
    {
        get
        {
            var h = new WindowInteropHelper(this).Handle;
            if (h == IntPtr.Zero)
            {
                var helper = new WindowInteropHelper(this);
                helper.EnsureHandle();
                h = helper.Handle;
            }
            return h;
        }
    }

    private void ApplyOverlayStyles()
    {
        var hwnd = Handle;
        if (hwnd == IntPtr.Zero) return;

        var ex = GetWindowLong(hwnd, GwlExstyle);
        ex |= WsExNoActivate | WsExToolWindow | WsExTopmost | WsExLayered | WsExTransparent;
        SetWindowLong(hwnd, GwlExstyle, ex);
        ForceTopmost();
    }

    private void ForceTopmost()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            SetWindowPos(
                hwnd,
                HwndTopMost,
                0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder | SwpShowWindow);
        }
        catch { }
    }

    public static void ShowTrack(string title, string artist)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_current is null)
            {
                _current = new ToastWindow();
                _ = _current.Handle; // create HWND
                _current.ApplyOverlayStyles();
            }

            var toast = _current;
            toast._hideTimer.Stop();

            toast.TitleText.Text = string.IsNullOrWhiteSpace(title) ? "Неизвестный трек" : title;
            toast.ArtistText.Text = string.IsNullOrWhiteSpace(artist) ? "Исполнитель не указан" : artist;

            // Prefer the monitor of the foreground fullscreen window if possible.
            PlaceTopRight(toast);

            if (!toast.IsVisible)
            {
                var hwnd = toast.Handle;
                ShowWindow(hwnd, SwShowNoActivate);
                toast.Visibility = Visibility.Visible;
                toast.ApplyOverlayStyles();
            }

            toast.UpdateLayout();
            PlaceTopRight(toast);
            toast.ForceTopmost();
            toast.FadeIn();
            toast._topmostTimer.Start();
            toast._hideTimer.Start();
        });
    }

    private static void PlaceTopRight(ToastWindow toast)
    {
        // Use virtual screen bounds so multi-monitor setups still get a corner overlay.
        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var width = SystemParameters.VirtualScreenWidth;
        // Default: top-right of primary work area (more predictable in games).
        var work = SystemParameters.WorkArea;
        toast.Left = work.Right - Math.Max(toast.ActualWidth, 320) - 16;
        toast.Top = work.Top + 16;

        // If content already measured, snap tightly.
        if (toast.ActualWidth > 50)
            toast.Left = work.Right - toast.ActualWidth - 16;
    }

    private void FadeIn()
    {
        RootBorder.BeginAnimation(UIElement.OpacityProperty, null);
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        RootBorder.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private async Task FadeOutAndHideAsync()
    {
        var tcs = new TaskCompletionSource();
        var anim = new DoubleAnimation(RootBorder.Opacity, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        anim.Completed += (_, _) => tcs.TrySetResult();
        RootBorder.BeginAnimation(UIElement.OpacityProperty, anim);
        await tcs.Task;
        try
        {
            _topmostTimer.Stop();
            Hide();
        }
        catch { }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyOverlayStyles();
    }
}
