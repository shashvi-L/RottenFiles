using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RottenFiles.Models;

public class FileCardViewModel : INotifyPropertyChanged
{
    private readonly FileRecord _record;
    private static readonly ConcurrentDictionary<string, ImageSource> _iconCache = new();
    private ImageSource? _fileIcon;

    public FileCardViewModel(FileRecord record) => _record = record;

    public int Id => _record.Id;
    public string FilePath => _record.FilePath;
    public string FileName => Path.GetFileName(_record.FilePath);
    public DateTime DateAdded => _record.DateAdded;

    public ImageSource FileIcon
    {
        get
        {
            if (_fileIcon != null) return _fileIcon;

            // Show placeholder immediately, load real icon in background
            _fileIcon = GetDefaultIcon();
            Task.Run(() => LoadIconAsync());
            return _fileIcon;
        }
    }

    public string RelativePath => GetRelativePath(_record.FilePath);

    private static string GetRelativePath(string fullPath)
    {
        try
        {
            string watched = Config.WatchedFolder;
            if (fullPath.StartsWith(watched, StringComparison.OrdinalIgnoreCase))
            {
                string relative = fullPath.Substring(watched.Length).TrimStart(Path.DirectorySeparatorChar);
                return relative;
            }
        }
        catch { }
        return Path.GetFileName(fullPath);
    }

    public string DisplayName => RelativePath;

    private async Task LoadIconAsync()
    {
        try
        {
            var icon = await Task.Run(() => ExtractIcon());
            if (icon != null)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    _fileIcon = icon;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileIcon)));
                });
            }
        }
        catch { }
    }

    private ImageSource? ExtractIcon()
    {
        string path = _record.FilePath;
        if (!File.Exists(path)) return null;

        // Cache by file extension (lowercase)
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (_iconCache.TryGetValue(ext, out var cached))
            return cached;

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon == null) return null;
            using var bmp = icon.ToBitmap();
            var stream = new MemoryStream();
            bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = stream;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze(); // allow cross‑thread access

            _iconCache[ext] = bitmapImage;
            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource GetDefaultIcon()
    {
        try
        {
            using var sysIcon = SystemIcons.Application;
            using var bmp = sysIcon.ToBitmap();
            var stream = new MemoryStream();
            bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = stream;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }
        catch
        {
            return new BitmapImage();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CountdownText
    {
        get
        {
            var remaining = _record.ExpiryDate - DateTime.UtcNow;
            if (remaining.TotalMinutes <= 0)
                return "Expired";
            if (remaining.TotalDays < 1)
                return $"Expires in {remaining.Hours}h {remaining.Minutes}m";
            return $"Expires in {(int)remaining.TotalDays} days";
        }
    }

    public SolidColorBrush CountdownColor
    {
        get
        {
            return _record.Urgency == FileUrgency.Rotten
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 50, 50))
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 179, 113));
        }
    }
}