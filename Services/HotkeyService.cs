using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace YandexMusicController.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001, ModControl = 0x0002, ModNoRepeat = 0x4000;
    private const uint VkLeft = 0x25, VkRight = 0x27, VkSpace = 0x20;
    private const int PreviousId = 1, NextId = 2, ToggleId = 3;

    private readonly Window _window;
    private HwndSource? _source;
    private readonly HashSet<int> _registered = [];
    private bool _disposed;
    private bool _registeredOnce;

    public string Status { get; private set; } = "Горячие клавиши ещё не зарегистрированы";

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public HotkeyService(Window window)
    {
        _window = window;
        // Ensure we register only after the HWND exists.
        _window.SourceInitialized += (_, _) =>
        {
            if (!_registeredOnce && !_disposed)
                Register();
        };
    }

    public void Register()
    {
        if (_disposed || _registered.Count > 0) return;

        var helper = new WindowInteropHelper(_window);
        helper.EnsureHandle(); // critical: force HWND creation if needed
        var handle = helper.Handle;
        if (handle == IntPtr.Zero)
        {
            Status = "Не удалось получить дескриптор окна для горячих клавиш";
            return;
        }

        _source = HwndSource.FromHwnd(handle);
        if (_source is null)
        {
            Status = "HwndSource недоступен";
            return;
        }

        _source.AddHook(Hook);

        var failed = new List<string>();
        RegisterOne(handle, PreviousId, VkLeft, "Ctrl+Alt+←", failed);
        RegisterOne(handle, NextId, VkRight, "Ctrl+Alt+→", failed);
        RegisterOne(handle, ToggleId, VkSpace, "Ctrl+Alt+Пробел", failed);

        _registeredOnce = true;
        Status = _registered.Count == 3
            ? "Все три комбинации активны"
            : $"Активно: {_registered.Count}/3. Не зарегистрированы: {string.Join(", ", failed)}";
    }

    private void RegisterOne(IntPtr handle, int id, uint key, string label, List<string> failed)
    {
        if (RegisterHotKey(handle, id, ModControl | ModAlt | ModNoRepeat, key))
            _registered.Add(id);
        else
        {
            var error = Marshal.GetLastWin32Error();
            failed.Add($"{label} (ошибка {error})");
        }
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey) return IntPtr.Zero;
        handled = true;
        switch (wParam.ToInt32())
        {
            case PreviousId: _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(MediaCommands.Previous); break;
            case NextId:     _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(MediaCommands.Next); break;
            case ToggleId:   _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(MediaCommands.Toggle); break;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            var handle = new WindowInteropHelper(_window).Handle;
            if (handle != IntPtr.Zero)
            {
                foreach (var id in _registered)
                    UnregisterHotKey(handle, id);
            }
        }
        catch { /* ignore */ }

        _registered.Clear();
        try { _source?.RemoveHook(Hook); } catch { }
        _source = null;
    }
}
