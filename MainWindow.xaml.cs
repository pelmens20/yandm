using System.ComponentModel;
using System.Windows;
using YandexMusicController.Services;

namespace YandexMusicController;

public partial class MainWindow : Window
{
    private readonly MediaSessionService _media = new();
    private readonly HotkeyService _hotkeys;
    private readonly HistoryService _history = new();
    private readonly TrayService _tray;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string _activeIdentity = "";
    private long _activeTrackId;
    private DateTime _lastSampleUtc;
    private bool _wasPlaying;
    private bool _allowClose;
    private bool _updatingStartup;
    private string _historySort = "recent";

    public MainWindow()
    {
        InitializeComponent();
        _hotkeys = new HotkeyService(this);
        MediaCommands.PreviousHandler = _media.PreviousAsync;
        MediaCommands.NextHandler = _media.NextAsync;
        MediaCommands.ToggleHandler = _media.TogglePlayPauseAsync;
        _tray = new TrayService(
            show: () => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); }),
            exit: () => Dispatcher.Invoke(ForceExit),
            history: () => Dispatcher.Invoke(() => _ = ShowHistoryViewAsync()),
            previous: () => _ = Dispatcher.InvokeAsync(MediaCommands.Previous),
            next: () => _ = Dispatcher.InvokeAsync(MediaCommands.Next),
            toggle: () => _ = Dispatcher.InvokeAsync(MediaCommands.Toggle));

        Loaded += async (_, _) =>
        {
            _updatingStartup = true;
            StartupCheck.IsChecked = StartupService.IsEnabled();
            _updatingStartup = false;

            _hotkeys.Register();
            HotkeyStatus.Text = _hotkeys.Status;

            _media.MediaChanged += OnMediaChanged;
            await _media.InitializeAsync();
            MediaSourceStatus.Text = _media.Status;
            await RefreshTrackAsync();
            _media.StartPolling();

            if (Environment.GetCommandLineArgs().Any(a => a.Equals("--background", StringComparison.OrdinalIgnoreCase)))
                Hide();
        };
    }

    private async void OnMediaChanged(object? sender, EventArgs e)
    {
        try { await Dispatcher.InvokeAsync(() => RefreshTrackAsync()).Task.Unwrap(); } catch { }
    }

    private async Task RefreshTrackAsync()
    {
        if (!await _refreshLock.WaitAsync(0)) return;
        try
        {
            var info = await _media.GetCurrentTrackAsync();
            var now = DateTime.UtcNow;
            var identity = info is null ? "" : $"{info.SourceAppId}|{info.Title}|{info.Artist}";

            // Accumulate listening time for the currently active unique track.
            if (_wasPlaying && _activeTrackId > 0)
            {
                var elapsed = (int)Math.Clamp((now - _lastSampleUtc).TotalSeconds, 0, 10);
                if (elapsed > 0) await _history.AddListeningTimeAsync(_activeTrackId, elapsed);
            }

            if (identity.Length > 0 && identity != _activeIdentity && !string.IsNullOrWhiteSpace(info!.Title))
            {
                _activeIdentity = identity;
                // One row per Title+Artist: find or create, bump play count.
                _activeTrackId = await _history.TouchTrackAsync(info.Title, info.Artist, now);
                // Always try hard overlay + native Windows toast.
                ToastWindow.ShowTrack(info.Title, info.Artist);
                TrackNotify.Show(info.Title, info.Artist);

                // Keep history grid fresh if the user is looking at it.
                if (HistoryView.Visibility == Visibility.Visible)
                    await LoadHistoryAsync();
            }
            else if (identity.Length == 0)
            {
                _activeIdentity = "";
                _activeTrackId = 0;
            }

            _wasPlaying = info?.IsPlaying == true;
            _lastSampleUtc = now;
            MediaSourceStatus.Text = _media.Status;
            if (info is null)
            {
                TrackTitle.Text = "Нет активного медиасеанса";
                TrackArtist.Text = "Запустите Яндекс Музыку и начните воспроизведение";
                PlaybackStatus.Text = "Ожидание медиасеанса";
                return;
            }

            TrackTitle.Text = string.IsNullOrWhiteSpace(info.Title) ? "Неизвестный трек" : info.Title;
            TrackArtist.Text = string.IsNullOrWhiteSpace(info.Artist) ? "Исполнитель не указан" : info.Artist;
            PlaybackStatus.Text = info.IsPlaying ? "▶ Воспроизводится" : "Ⅱ На паузе";
            FooterStatus.Text = "Обновлено " + DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex) { FooterStatus.Text = "Ошибка обновления: " + ex.Message; }
        finally { _refreshLock.Release(); }
    }

    // --- Navigation ---
    private void NavPlayer_Click(object sender, RoutedEventArgs e) => ShowPlayerView();
    private async void NavHistory_Click(object sender, RoutedEventArgs e) => await ShowHistoryViewAsync();

    private void ShowPlayerView()
    {
        PlayerView.Visibility = Visibility.Visible;
        HistoryView.Visibility = Visibility.Collapsed;
        SetNavActive(NavPlayerBtn, NavHistoryBtn);
    }

    private async Task ShowHistoryViewAsync()
    {
        PlayerView.Visibility = Visibility.Collapsed;
        HistoryView.Visibility = Visibility.Visible;
        SetNavActive(NavHistoryBtn, NavPlayerBtn);
        await LoadHistoryAsync();
    }

    private static void SetNavActive(System.Windows.Controls.Button active, System.Windows.Controls.Button inactive)
    {
        active.Style = (Style)System.Windows.Application.Current.Resources["AccentButton"];
        inactive.Style = (Style)System.Windows.Application.Current.Resources["GhostButton"];
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var items = await _history.GetTracksAsync(_historySort);
            var (tracks, plays, totalSec) = await _history.GetSummaryAsync();
            HistoryGrid.ItemsSource = items;
            HistorySummary.Text = $"Уникальных треков: {tracks}   •   Прослушиваний: {plays}   •   Время: {Models.HistoryItem.FormatDuration(totalSec)}";
        }
        catch (Exception ex)
        {
            HistorySummary.Text = "Ошибка загрузки истории: " + ex.Message;
        }
    }

    private async void HistoryRefresh_Click(object sender, RoutedEventArgs e) => await LoadHistoryAsync();
    private async void HistorySortRecent_Click(object sender, RoutedEventArgs e) { _historySort = "recent"; await LoadHistoryAsync(); }
    private async void HistorySortPlays_Click(object sender, RoutedEventArgs e) { _historySort = "plays"; await LoadHistoryAsync(); }
    private async void HistorySortTime_Click(object sender, RoutedEventArgs e) { _historySort = "time"; await LoadHistoryAsync(); }

    // --- Controls ---
    private async void Previous_Click(object sender, RoutedEventArgs e) => await _media.PreviousAsync();
    private async void Next_Click(object sender, RoutedEventArgs e) => await _media.NextAsync();
    private async void PlayPause_Click(object sender, RoutedEventArgs e) => await _media.TogglePlayPauseAsync();

    private void StartupCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingStartup) return;
        try
        {
            StartupService.SetEnabled(StartupCheck.IsChecked == true);
            FooterStatus.Text = StartupCheck.IsChecked == true ? "Автозапуск включён" : "Автозапуск выключен";
        }
        catch (Exception ex)
        {
            FooterStatus.Text = "Не удалось изменить автозапуск: " + ex.Message;
            _updatingStartup = true;
            StartupCheck.IsChecked = StartupService.IsEnabled();
            _updatingStartup = false;
        }
    }

    private void MinimizeToTray_Click(object sender, RoutedEventArgs e) => Hide();

    internal void AllowCloseForShutdown() => _allowClose = true;

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized) Hide();
    }

    private void ForceExit()
    {
        _allowClose = true;
        try
        {
            _media.MediaChanged -= OnMediaChanged;
            _hotkeys.Dispose();
            _media.Dispose();
            _tray.Dispose();
            _refreshLock.Dispose();
        }
        catch { }
        MediaCommands.PreviousHandler = null;
        MediaCommands.NextHandler = null;
        MediaCommands.ToggleHandler = null;
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Close button (X) now fully exits the app.
        // Use "Свернуть в трей" if you want it to keep running.
        if (!_allowClose)
        {
            _allowClose = true;
        }
        try
        {
            _media.MediaChanged -= OnMediaChanged;
            _hotkeys.Dispose();
            _media.Dispose();
            _tray.Dispose();
            _refreshLock.Dispose();
        }
        catch { }
        MediaCommands.PreviousHandler = null;
        MediaCommands.NextHandler = null;
        MediaCommands.ToggleHandler = null;
        base.OnClosing(e);
        System.Windows.Application.Current.Shutdown();
    }
}

