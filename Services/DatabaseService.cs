using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using RottenFiles.Models;

namespace RottenFiles.Services;

public class DatabaseService : IDisposable
{
    private readonly SqliteConnection _connection;

    public DatabaseService()
    {
        string dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RottenFiles", "rottenfiles.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Files (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FilePath TEXT UNIQUE NOT NULL,
                DateAdded TEXT NOT NULL,
                Status TEXT DEFAULT 'Active',
                Notified24h INTEGER DEFAULT 0
            )";
        cmd.ExecuteNonQuery();
    }

    public void DeactivateAllActiveFiles()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE Files SET Status = 'Deleted' WHERE Status = 'Active'";
        cmd.ExecuteNonQuery();
    }

    public void AddFile(string filePath)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO Files (FilePath, DateAdded) VALUES (@path, @date)";
        cmd.Parameters.AddWithValue("@path", filePath);
        cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    

    public List<FileRecord> GetActiveFiles()
    {
        var files = new List<FileRecord>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, FilePath, DateAdded, Status, Notified24h FROM Files WHERE Status = 'Active'";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            files.Add(new FileRecord
            {
                Id = reader.GetInt32(0),
                FilePath = reader.GetString(1),
                DateAdded = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                Status = reader.GetString(3),
                Notified24h = reader.GetBoolean(4)
            });
        }
        return files;
    }

    /// <summary>
    /// Returns all file records regardless of status.
    /// </summary>
    public List<FileRecord> GetAllDbFiles()
    {
        var files = new List<FileRecord>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, FilePath, DateAdded, Status, Notified24h FROM Files";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            files.Add(new FileRecord
            {
                Id = reader.GetInt32(0),
                FilePath = reader.GetString(1),
                DateAdded = DateTime.Parse(reader.GetString(2)),
                Status = reader.GetString(3),
                Notified24h = reader.GetBoolean(4)
            });
        }
        return files;
    }

    /// <summary>
    /// Reactivates a previously deleted/kept file: resets its date and status to Active.
    /// </summary>
    public void ReactivateFile(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"UPDATE Files 
                            SET Status = 'Active', 
                                DateAdded = @date, 
                                Notified24h = 0 
                            WHERE Id = @id";
        cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void UpdateFileStatus(int id, string status)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE Files SET Status = @status WHERE Id = @id";
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetNotified(int id, bool value = true)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE Files SET Notified24h = @val WHERE Id = @id";
        cmd.Parameters.AddWithValue("@val", value ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteRecord(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Files WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
    /// <summary>
    /// Deletes all rows from the Files table. Safe to call while the database is open.
    /// </summary>
    public void WipeAllFiles()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Files";
        cmd.ExecuteNonQuery();
    }

}