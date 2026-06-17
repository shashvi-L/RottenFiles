using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using RottenFiles.Models;
using RottenFiles.Services;

namespace RottenFiles;

public partial class MainWindow : Window
{
    private TrayIconService? _trayService;
    private DatabaseService _db;
    private FileWatcherService? _fileWatcher;
    private NotificationService? _notificationService;
    private DispatcherTimer? _trayTimer;
    private Icon? _currentTrayIcon;
    private DispatcherTimer? _listTimer;

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    public MainWindow()
    {
        InitializeComponent();

        Config.Load();

        _db = new DatabaseService();
        _notificationService = new NotificationService(_db);

        // Tray service
        _trayService = new TrayIconService();
        _trayService.OpenWindowRequested += () => Dispatcher.Invoke(ShowMainWindow);
        _trayService.SettingsRequested += () => Dispatcher.Invoke(OpenSettings);
        _trayService.QuitRequested += () => Dispatcher.Invoke(Shutdown);
        _trayService.Start();

        // File watcher (no FilesChanged event used)
        _fileWatcher = new FileWatcherService(_db, _notificationService, Config.WatchedFolder);
        _fileWatcher.Start();

        // Tray icon & notifications every 5 seconds
        _trayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _trayTimer.Tick += (s, e) =>
        {
            UpdateTrayState();
            _notificationService.CheckAndNotify();
        };
        _trayTimer.Start();

        // Periodic scan + list refresh every 30 seconds
        _listTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _listTimer.Tick += (s, e) =>
        {
            _fileWatcher?.ScanNow();
            RefreshFileList();
        };
        _listTimer.Start();

        // Initial UI update
        UpdateTrayState();
        RefreshFileList();

        this.Hide();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _fileWatcher?.ScanNow();
        RefreshFileList();
    }

    private void UpdateTrayState()
    {
        if (_trayService == null) return;

        var files = _db.GetActiveFiles();
        // Files expiring within 24 hours (includes already expired)
        // Only files that are still active and will expire within the next 24 hours
        int urgentCount = files.Count(f =>
            f.ExpiryDate > DateTime.Now && f.ExpiryDate <= DateTime.Now.AddHours(24));

        var newIcon = CreateNumberedIcon(GetIconSafe("rotten.ico"), urgentCount);
        if (_currentTrayIcon != null && _currentTrayIcon != newIcon)
            _currentTrayIcon.Dispose();
        _currentTrayIcon = newIcon;
        _trayService.SetIcon(newIcon);
        _trayService.SetToolTip($"RottenFiles - {urgentCount} file(s) expiring within 24h");
    }

    private Icon CreateNumberedIcon(Icon baseIcon, int count)
    {
        if (count <= 0) return (Icon)baseIcon.Clone();

        using (System.Drawing.Bitmap bmp = baseIcon.ToBitmap())
        {
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
            {
                int iconSize = bmp.Width;
                int badgeRadius = iconSize / 3;
                int margin = 1;
                System.Drawing.Point badgeCenter = new System.Drawing.Point(
                    iconSize - badgeRadius - margin,
                    iconSize - badgeRadius - margin);

                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (System.Drawing.Brush redBrush = new System.Drawing.SolidBrush(
                    System.Drawing.Color.FromArgb(220, 50, 50)))
                {
                    g.FillEllipse(redBrush,
                        badgeCenter.X - badgeRadius, badgeCenter.Y - badgeRadius,
                        badgeRadius * 2, badgeRadius * 2);
                }

                string text = count > 99 ? "99+" : count.ToString();
                using (System.Drawing.Font font = new System.Drawing.Font(
                    "Segoe UI", badgeRadius * 1.3f, System.Drawing.FontStyle.Bold))
                using (System.Drawing.Brush whiteBrush = new System.Drawing.SolidBrush(
                    System.Drawing.Color.White))
                {
                    System.Drawing.SizeF textSize = g.MeasureString(text, font);
                    float x = badgeCenter.X - textSize.Width / 2;
                    float y = badgeCenter.Y - textSize.Height / 2;
                    g.DrawString(text, font, whiteBrush, x, y);
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            Icon result = (Icon)System.Drawing.Icon.FromHandle(hIcon).Clone();
            DestroyIcon(hIcon);
            return result;
        }
    }

    private Icon GetIconSafe(string name)
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            // The resource name is: <DefaultNamespace>.Resources.Icons.<filename>
            // The default namespace is usually the project name: "RottenFiles"
            string resourceName = $"RottenFiles.Resources.Icons.{name}";
            using (var stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                    return new Icon(stream);
            }
        }
        catch { }
        return SystemIcons.Application;
    }

    private void ShowMainWindow()
    {
        _fileWatcher?.ScanNow();
        RefreshFileList();
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
    }

    private void OpenSettings()
    {
        string oldFolder = Config.WatchedFolder;
        var settings = new SettingsWindow();
        bool? saved = settings.ShowDialog();
        if (saved == true)
        {
            if (!string.Equals(Config.WatchedFolder, oldFolder, StringComparison.OrdinalIgnoreCase))
            {
                // 1. Stop the current file watcher
                _fileWatcher?.Dispose();
                _fileWatcher = null;

                // 2. Wipe all tracked files from the database (no file lock issues)
                _db?.WipeAllFiles();

                // 3. Dispose the old database connection (optional, for cleanliness)
                _db?.Dispose();
                _db = new DatabaseService();
                _notificationService = new NotificationService(_db);

                // 4. Start a fresh file watcher on the new folder
                _fileWatcher = new FileWatcherService(_db, _notificationService, Config.WatchedFolder);
                _fileWatcher.Start();
            }

            // Rescan and refresh UI
            _fileWatcher?.ScanNow();
            RefreshFileList();
            UpdateTrayState();
        }
    }

    private void Shutdown()
    {
        _trayTimer?.Stop();
        _listTimer?.Stop();
        _currentTrayIcon?.Dispose();
        _trayService?.Dispose();
        _fileWatcher?.Dispose();
        _db?.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        this.Hide();
    }

    private void RefreshFileList()
    {
        var files = _db.GetActiveFiles();
        files.Sort((a, b) =>
        {
            if (a.Urgency != b.Urgency) return a.Urgency.CompareTo(b.Urgency);
            return a.ExpiryDate.CompareTo(b.ExpiryDate);
        });

        var viewModels = files.Select(f => new FileCardViewModel(f)).ToList();
        FileCardsControl.ItemsSource = viewModels;
    }

    private void Keep_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is FileCardViewModel card)
        {
            var dialog = new SaveFileDialog
            {
                FileName = card.FileName,
                Title = "Choose where to save the file"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.Move(card.FilePath, dialog.FileName);
                    _db.UpdateFileStatus(card.Id, "Moved");
                    RefreshFileList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Move failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void DeleteNow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is FileCardViewModel card)
        {
            var result = MessageBox.Show($"Delete '{card.FileName}' permanently?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (File.Exists(card.FilePath))
                        File.Delete(card.FilePath);
                    _db.UpdateFileStatus(card.Id, "Deleted");
                    RefreshFileList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Delete failed: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }
}