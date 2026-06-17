using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace RottenFiles.Services;

public class TrayIconService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private Thread? _trayThread;
    private readonly object _lock = new();
    private Icon _currentIcon = SystemIcons.Application;
    private string _tooltip = "RottenFiles";
    private string[]? _menuActions; // null until set

    public event Action? OpenWindowRequested;
    public event Action? SettingsRequested;
    public event Action? QuitRequested;

    public void Start()
    {
        _trayThread = new Thread(RunTray)
        {
            IsBackground = true,
            Name = "TrayThread"
        };
        _trayThread.SetApartmentState(ApartmentState.STA);
        _trayThread.Start();
    }

    private void RunTray()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = _currentIcon,
            Text = _tooltip,
            Visible = true
        };

        // Build context menu
        var menu = new ContextMenuStrip();
        var openItem = menu.Items.Add("Open RottenFiles");
        openItem.Click += (s, e) => OpenWindowRequested?.Invoke();
        var settingsItem = menu.Items.Add("Settings");
        settingsItem.Click += (s, e) => SettingsRequested?.Invoke();
        menu.Items.Add(new ToolStripSeparator());
        var quitItem = menu.Items.Add("Quit");
        quitItem.Click += (s, e) => QuitRequested?.Invoke();

        _notifyIcon.ContextMenuStrip = menu;

        // Only open the main window on a left‑click (not right‑click)
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
                OpenWindowRequested?.Invoke();
        };

        Application.Run();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    public void SetIcon(Icon icon)
    {
        lock (_lock)
        {
            if (_notifyIcon != null)
                _notifyIcon.Icon = icon;

            // Dispose the old icon (but never dispose the fallback default)
            if (_currentIcon != null && _currentIcon != SystemIcons.Application && _currentIcon != icon)
                _currentIcon.Dispose();

            _currentIcon = icon;
        }
    }

    public void SetToolTip(string tooltip)
    {
        lock (_lock)
        {
            _tooltip = tooltip;
            if (_notifyIcon != null)
                _notifyIcon.Text = tooltip;
        }
    }

    public void Dispose()
    {
        if (_trayThread != null && _trayThread.IsAlive)
        {
            Application.ExitThread();
            _trayThread.Join(2000);
        }
        _notifyIcon?.Dispose();
    }
    public void ShowBalloonTip(string title, string text)
    {
        lock (_lock)
        {
            _notifyIcon?.ShowBalloonTip(5000, title, text, ToolTipIcon.Info);
        }
    }

}