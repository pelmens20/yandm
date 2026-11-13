using System.Drawing;
using System.Windows.Forms;

namespace YandexMusicController.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Action _show, _exit, _history, _previous, _next, _toggle;
    public bool IsExiting { get; private set; }

    public TrayService(Action show, Action exit, Action history, Action previous, Action next, Action toggle)
    {
        _show = show;
        _exit = () => { IsExiting = true; exit(); };
        _history = history;
        _previous = previous;
        _next = next;
        _toggle = toggle;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => _show());
        menu.Items.Add("Предыдущий трек", null, (_, _) => _previous());
        menu.Items.Add("Play / Pause", null, (_, _) => _toggle());
        menu.Items.Add("Следующий трек", null, (_, _) => _next());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("История", null, (_, _) => _history());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => _exit());

        _icon = new NotifyIcon
        {
            Text = "Yandex Music Controller",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu
        };
        _icon.DoubleClick += (_, _) => _show();
    }

    public void ShowTrack(string title, string artist)
    {
        // Balloon near the tray — more reliable over fullscreen/borderless games.
        _icon.BalloonTipTitle = string.IsNullOrWhiteSpace(title) ? "Трек" : title;
        _icon.BalloonTipText = string.IsNullOrWhiteSpace(artist) ? "Яндекс Музыка" : artist;
        _icon.BalloonTipIcon = ToolTipIcon.Info;
        _icon.ShowBalloonTip(3500);
        _icon.Text = Truncate($"{title} — {artist}", 60);
    }

    public void ShowBalloon(string title, string message)
    {
        _icon.ShowBalloonTip(2500, title, message, ToolTipIcon.Info);
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[.. (max - 1)] + "…");

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
