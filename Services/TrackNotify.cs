using Microsoft.Toolkit.Uwp.Notifications;

namespace YandexMusicController.Services;

/// <summary>
/// Native Windows notification (Action Center / banner).
/// More reliable over games like Dota 2 than custom WPF windows.
/// </summary>
public static class TrackNotify
{
    public static void Show(string title, string artist)
    {
        try
        {
            try { ToastNotificationManagerCompat.History.Clear(); } catch { }

            new ToastContentBuilder()
                .AddArgument("action", "viewTrack")
                .AddText(string.IsNullOrWhiteSpace(title) ? "Неизвестный трек" : title, hintMaxLines: 2)
                .AddText(string.IsNullOrWhiteSpace(artist) ? "Яндекс Музыка" : artist, hintMaxLines: 1)
                .AddText("Yandex Music Controller", hintStyle: AdaptiveTextStyle.CaptionSubtle)
                .SetToastDuration(ToastDuration.Long)
                .SetToastScenario(ToastScenario.Default)
                .Show(toast =>
                {
                    toast.ExpirationTime = DateTimeOffset.Now.AddSeconds(6);
                    toast.Tag = "ymc-track";
                    toast.Group = "ymc";
                });
        }
        catch
        {
            // Caller may still show tray balloon / custom toast.
        }
    }
}
