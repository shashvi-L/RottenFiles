using System;

namespace RottenFiles.Models;

public enum FileUrgency
{
    Ripe,
    Overripe,
    Rotten
}

public class FileRecord
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DateTime DateAdded { get; set; }
    public string Status { get; set; } = "Active";
    public bool Notified24h { get; set; } = false;

    public DateTime ExpiryDate => DateAdded.AddDays(Config.ExpiryDays);

    public FileUrgency Urgency
    {
        get
        {
            var remaining = ExpiryDate.Date - DateTime.Today;
            if (remaining.TotalDays <= 0) return FileUrgency.Rotten;
            if (remaining.TotalDays <= Config.OverripeDays) return FileUrgency.Overripe;
            return FileUrgency.Ripe;
        }
    }
}