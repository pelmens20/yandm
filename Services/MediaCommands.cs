namespace YandexMusicController.Services;

// Shared command bridge keeps global hotkeys independent from the main window's button handlers.
public static class MediaCommands
{
    public static Func<Task>? PreviousHandler { get; set; }
    public static Func<Task>? NextHandler { get; set; }
    public static Func<Task>? ToggleHandler { get; set; }
    public static Task Previous() => PreviousHandler?.Invoke() ?? Task.CompletedTask;
    public static Task Next() => NextHandler?.Invoke() ?? Task.CompletedTask;
    public static Task Toggle() => ToggleHandler?.Invoke() ?? Task.CompletedTask;
}
