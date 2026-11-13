namespace YandexMusicController.Models;

public sealed record TrackInfo(string Title, string Artist, bool IsPlaying, string SourceAppId);

public sealed class HistoryItem
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public int PlayCount { get; set; }
    public long TotalSeconds { get; set; }
    public string TotalTime => FormatDuration(TotalSeconds);
    public DateTime LastPlayedUtc { get; set; }
    public string LastPlayedLocal => LastPlayedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
    public static string FormatDuration(long seconds) => seconds < 60
        ? $"{seconds} сек."
        : seconds < 3600 ? $"{seconds / 60} мин. {seconds % 60} сек."
        : $"{seconds / 3600} ч. {(seconds % 3600) / 60} мин.";
}
