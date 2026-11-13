using System.Threading;
using System.Windows;

namespace YandexMusicController;

public partial class App : System.Windows.Application
{
    private Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(true, @"Local\YandexMusicController.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Allow clean exit when Windows is shutting down / user is logging off.
        SessionEnding += (_, args) =>
        {
            if (MainWindow is MainWindow mw)
                mw.AllowCloseForShutdown();
        };

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _instanceMutex?.ReleaseMutex(); } catch { }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
