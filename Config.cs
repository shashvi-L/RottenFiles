using System;
using System.IO;
using System.Text.Json;

namespace RottenFiles;

public static class Config
{
    private static readonly string AppDataPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RottenFiles");

    public static string SettingsFile => Path.Combine(AppDataPath, "settings.json");

    // Default values
    public static int ExpiryDays { get; set; } = 7;
    public static int OverripeDays { get; set; } = 2;
    public static NotificationMode NotificationMode { get; set; } = NotificationMode.DailyDigest;
    public static int CustomNotificationDays { get; set; } = 3;
    public static string WatchedFolder { get; set; } = @"C:\RottenFiles";
    public static string? DigestTime { get; set; } = "09:00";
    public static string? LastDigestDate { get; set; } = null;

    public static void Load()
    {
        Directory.CreateDirectory(AppDataPath);
        if (File.Exists(SettingsFile))
        {
            var json = File.ReadAllText(SettingsFile);
            var cfg = JsonSerializer.Deserialize<SettingsData>(json);
            if (cfg != null)
            {
                ExpiryDays = cfg.ExpiryDays;
                OverripeDays = cfg.OverripeDays;
                NotificationMode = cfg.NotificationMode;
                CustomNotificationDays = cfg.CustomNotificationDays;
                WatchedFolder = cfg.WatchedFolder;
                DigestTime = cfg.DigestTime;
                LastDigestDate = cfg.LastDigestDate;
            }
        }
        else
        {
            Save(); // create default settings file on first run
        }
    }

    public static void Save()
    {
        var data = new SettingsData
        {
            ExpiryDays = ExpiryDays,
            OverripeDays = OverripeDays,
            NotificationMode = NotificationMode,
            CustomNotificationDays = CustomNotificationDays,
            WatchedFolder = WatchedFolder,
            DigestTime = DigestTime,
            LastDigestDate = LastDigestDate
        };
        File.WriteAllText(SettingsFile,
            JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    // Internal helper class used only for JSON serialization
    private class SettingsData
    {
        public int ExpiryDays { get; set; }
        public int OverripeDays { get; set; }
        public NotificationMode NotificationMode { get; set; }
        public int CustomNotificationDays { get; set; }
        public string WatchedFolder { get; set; } = string.Empty;
        public string? DigestTime { get; set; }
        public string? LastDigestDate { get; set; }
    }
}

public enum NotificationMode
{
    None,
    TwentyFourHours,
    FiveDays,
    DailyDigest,
    CustomOffset
}