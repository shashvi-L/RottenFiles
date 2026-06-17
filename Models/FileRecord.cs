using System;

namespace RottenFiles.Models;

public enum FileUrgency
{
    Ripe,
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
            if (ExpiryDate <= DateTime.UtcNow) return FileUrgency.Rotten;
            return FileUrgency.Ripe;
        }
    }
}