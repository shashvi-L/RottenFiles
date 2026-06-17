using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RottenFiles.Models;

namespace RottenFiles.Services;

public class NotificationService
{
    private readonly DatabaseService _db;

    /// <summary>Fires when a notification should be shown (title, body).</summary>
    public event Action<string, string>? OnNotificationReady;

    public NotificationService(DatabaseService db) => _db = db;

    public void CheckAndNotify()
    {
        if (Config.NotificationMode == NotificationMode.None) return;

        var activeFiles = _db.GetActiveFiles();

        switch (Config.NotificationMode)
        {
            case NotificationMode.TwentyFourHours:
                NotifyBeforeExpiry(activeFiles, TimeSpan.FromHours(24));
                break;
            case NotificationMode.FiveDays:
                NotifyBeforeExpiry(activeFiles, TimeSpan.FromDays(5));
                break;
            case NotificationMode.CustomOffset:
                NotifyBeforeExpiry(activeFiles, TimeSpan.FromDays(Config.CustomNotificationDays));
                break;
            case NotificationMode.DailyDigest:
                SendDailyDigest(activeFiles);
                break;
        }
    }

    private void NotifyBeforeExpiry(List<FileRecord> files, TimeSpan offset)
    {
        foreach (var file in files)
        {
            var notifyTime = file.ExpiryDate - offset;
            if (notifyTime <= DateTime.UtcNow && !file.Notified24h)
            {
                var remaining = file.ExpiryDate - DateTime.UtcNow;
                var hours = Math.Max(0, Math.Round(remaining.TotalHours));
                string title = $"Expiring soon: {Path.GetFileName(file.FilePath)}";
                string body = $"Expires in {hours} hour(s).";

                OnNotificationReady?.Invoke(title, body);
                _db.SetNotified(file.Id);
            }
        }
    }

    private void SendDailyDigest(List<FileRecord> files)
    {
        if (string.IsNullOrEmpty(Config.DigestTime)) return;

        var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
        if (Config.LastDigestDate == todayStr) return;

        var digestTime = DateTime.Today.Add(TimeSpan.Parse(Config.DigestTime));
        if (DateTime.UtcNow < digestTime) return;

        var rotten = files.Count(f => f.Urgency == FileUrgency.Rotten);
        if (rotten == 0) return;

        string title = "RottenFiles Daily Digest";
        string body = $"You have {rotten} rotten file(s).";

        OnNotificationReady?.Invoke(title, body);

        Config.LastDigestDate = todayStr;
        Config.Save();
    }
}