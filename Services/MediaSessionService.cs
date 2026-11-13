using Windows.Media.Control;
using YandexMusicController.Models;

namespace YandexMusicController.Services;

public sealed class MediaSessionService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private CancellationTokenSource? _pollCts;
    private string _lastSignature = "";
    private GlobalSystemMediaTransportControlsSession? _selectedSession;

    public event EventHandler? MediaChanged;
    public string Status { get; private set; } = "Инициализация медиасеансов Windows…";

    public async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += (_, _) => RaiseChanged();
            _manager.SessionsChanged += (_, _) => RaiseChanged();
            Status = "Медиасеансы Windows доступны";
        }
        catch (UnauthorizedAccessException)
        {
            Status = "Нет доступа к медиасеансам (запустите приложение обычным пользователем, не из повышенных прав)";
        }
        catch (Exception ex)
        {
            Status = "Не удалось подключить медиасеансы: " + ex.Message;
        }
    }

    private GlobalSystemMediaTransportControlsSession? SelectSession()
    {
        if (_manager is null) return null;

        var sessions = _manager.GetSessions();
        // Strictly target Yandex Music; do not accidentally control a browser or another player.
        _selectedSession = sessions.FirstOrDefault(s =>
        {
            var id = s.SourceAppUserModelId ?? "";
            return id.Contains("yandex", StringComparison.OrdinalIgnoreCase)
                || id.Contains("Yandex.Music", StringComparison.OrdinalIgnoreCase);
        });
        return _selectedSession;
    }

    public void StartPolling()
    {
        if (_pollCts is not null) return;
        var cts = new CancellationTokenSource();
        _pollCts = cts;

        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var track = await GetCurrentTrackAsync();
                    var signature = track is null
                        ? ""
                        : $"{track.SourceAppId}|{track.Title}|{track.Artist}|{track.IsPlaying}";
                    if (signature != _lastSignature)
                    {
                        _lastSignature = signature;
                        RaiseChanged();
                    }
                }
                catch { /* ignore transient errors */ }

                try { await Task.Delay(1200, cts.Token); }
                catch (OperationCanceledException) { break; }
            }
        });
    }

    public async Task<TrackInfo?> GetCurrentTrackAsync()
    {
        try
        {
            var session = SelectSession();
            if (session is null)
            {
                Status = "Медиасеанс не найден. Запустите Яндекс Музыку.";
                return null;
            }

            var props = await session.TryGetMediaPropertiesAsync();
            var playbackInfo = session.GetPlaybackInfo();
            var state = playbackInfo?.PlaybackStatus;

            Status = "Источник: " + (session.SourceAppUserModelId ?? "unknown");
            return new TrackInfo(
                props?.Title ?? "",
                props?.Artist ?? "",
                state == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                session.SourceAppUserModelId ?? "");
        }
        catch (Exception ex)
        {
            Status = "Ошибка чтения медиасеанса: " + ex.Message;
            return null;
        }
    }

    public async Task PreviousAsync() => await SendAsync(s => s.TrySkipPreviousAsync());
    public async Task NextAsync() => await SendAsync(s => s.TrySkipNextAsync());
    public async Task TogglePlayPauseAsync() => await SendAsync(s => s.TryTogglePlayPauseAsync());

    private async Task SendAsync(Func<GlobalSystemMediaTransportControlsSession, Windows.Foundation.IAsyncOperation<bool>> command)
    {
        try
        {
            var session = SelectSession();
            if (session is null)
            {
                Status = "Нет доступного медиасеанса";
                RaiseChanged();
                return;
            }

            var success = await command(session);
            Status = success
                ? "Команда отправлена: " + (session.SourceAppUserModelId ?? "")
                : "Приложение не приняло команду";
        }
        catch (Exception ex)
        {
            Status = "Ошибка команды: " + ex.Message;
        }
        RaiseChanged();
    }

    private void RaiseChanged() => MediaChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }
}
