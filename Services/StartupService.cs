using System.IO;
using Microsoft.Win32;

namespace YandexMusicController.Services;

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "YandexMusicController";
    public static bool IsEnabled()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(RunKey, false); return key?.GetValue(ValueName) is string; }
        catch { return false; }
    }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled)
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) throw new InvalidOperationException("Не удалось определить путь к EXE. Запустите опубликованную версию программы.");
            key.SetValue(ValueName, $"\"{exe}\" --background");
        }
        else key.DeleteValue(ValueName, false);
    }
}
