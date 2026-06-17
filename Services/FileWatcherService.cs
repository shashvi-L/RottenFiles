using System;
using System.IO;
using System.Linq;
using System.Timers;
using RottenFiles.Models;

namespace RottenFiles.Services;

public class FileWatcherService : IDisposable
{
    private readonly DatabaseService _db;
    private readonly NotificationService _notifier;
    private FileSystemWatcher? _watcher;
    private System.Timers.Timer? _autoDeleteTimer;
    public string WatchedFolder { get; private set; }

    public FileWatcherService(DatabaseService db, NotificationService notifier, string folder)
    {
        _db = db;
        _notifier = notifier;
        WatchedFolder = folder;
    }

    public void Start()
    {
        Directory.CreateDirectory(WatchedFolder);

        // Real‑time watcher
        _watcher = new FileSystemWatcher(WatchedFolder)
        {
            NotifyFilter = NotifyFilters.FileName,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            InternalBufferSize = 65536   // 64 KB
        };
        _watcher.Created += OnFileCreated;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Error += OnWatcherError;

        // Auto‑delete every 10 minutes
        _autoDeleteTimer = new System.Timers.Timer(600_000);
        _autoDeleteTimer.Elapsed += OnAutoDeleteTimerElapsed;
        _autoDeleteTimer.AutoReset = true;
        _autoDeleteTimer.Start();

        // Initial reconciliation
        ReconcileFolder();
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (File.Exists(e.FullPath))
            _db.AddFile(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (File.Exists(e.FullPath))
            _db.AddFile(e.FullPath);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        // Watcher overflow – restart it. The periodic UI scan will catch missed files.
        try
        {
            _watcher?.Dispose();
            _watcher = new FileSystemWatcher(WatchedFolder)
            {
                NotifyFilter = NotifyFilters.FileName,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
                InternalBufferSize = 65536
            };
            _watcher.Created += OnFileCreated;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Error += OnWatcherError;
        }
        catch { }
    }

    private void OnAutoDeleteTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        _notifier.CheckAndNotify();
        DeleteExpiredFiles();
    }

    private void DeleteExpiredFiles()
    {
        var activeFiles = _db.GetActiveFiles();
        foreach (var file in activeFiles)
        {
            if (file.ExpiryDate <= DateTime.UtcNow)
            {
                try
                {
                    if (File.Exists(file.FilePath))
                        File.Delete(file.FilePath);
                    _db.UpdateFileStatus(file.Id, "Deleted");
                }
                catch { /* will retry */ }
            }
        }
    }

    /// <summary>Force a folder scan and DB reconciliation. Safe to call from any thread.</summary>
    public void ScanNow()
    {
        ReconcileFolder();
    }

    private void ReconcileFolder()
    {
        try
        {
            var diskFiles = Directory.GetFiles(WatchedFolder, "*", SearchOption.AllDirectories);
            var allDbFiles = ((DatabaseService)_db).GetAllDbFiles();
            var dbPaths = allDbFiles.Select(f => f.FilePath).ToHashSet();

            foreach (var diskFile in diskFiles)
            {
                if (!dbPaths.Contains(diskFile))
                {
                    _db.AddFile(diskFile);
                }
                else
                {
                    var existing = allDbFiles.FirstOrDefault(f => f.FilePath == diskFile);
                    if (existing != null && existing.Status != "Active")
                    {
                        _db.ReactivateFile(existing.Id);
                    }
                }
            }

            var activeFiles = _db.GetActiveFiles();
            foreach (var file in activeFiles)
            {
                if (!File.Exists(file.FilePath))
                    _db.UpdateFileStatus(file.Id, "Deleted");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ReconcileFolder error: {ex.Message}");
        }
    }
    public void Dispose()
    {
        _watcher?.Dispose();
        _autoDeleteTimer?.Dispose();
    }
}