using System.IO;
using Microsoft.Data.Sqlite;
using YandexMusicController.Models;

namespace YandexMusicController.Services;

public sealed class HistoryService
{
    private readonly string _dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YandexMusicController", "history.db");
    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _dbPath, Pooling = true }.ToString();

    public HistoryService()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        using var db = new SqliteConnection(ConnectionString);
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS Tracks(
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              Title TEXT NOT NULL,
              Artist TEXT NOT NULL,
              PlayCount INTEGER NOT NULL DEFAULT 1,
              TotalSeconds INTEGER NOT NULL DEFAULT 0,
              FirstPlayedUtc TEXT NOT NULL,
              LastPlayedUtc TEXT NOT NULL,
              UNIQUE(Title, Artist)
            );
            CREATE INDEX IF NOT EXISTS IX_Tracks_LastPlayedUtc ON Tracks(LastPlayedUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_Tracks_TotalSeconds ON Tracks(TotalSeconds DESC);
            """;
        cmd.ExecuteNonQuery();

        // One-time migration from old History table (if present).
        try
        {
            using var check = db.CreateCommand();
            check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='History'";
            var hasOld = check.ExecuteScalar() is string;
            if (hasOld)
            {
                using var migrate = db.CreateCommand();
                migrate.CommandText = """
                    INSERT INTO Tracks(Title, Artist, PlayCount, TotalSeconds, FirstPlayedUtc, LastPlayedUtc)
                    SELECT Title, Artist,
                           COUNT(*) AS PlayCount,
                           COALESCE(SUM(DurationSeconds), 0) AS TotalSeconds,
                           MIN(PlayedAtUtc) AS FirstPlayedUtc,
                           MAX(PlayedAtUtc) AS LastPlayedUtc
                    FROM History
                    GROUP BY Title, Artist
                    ON CONFLICT(Title, Artist) DO UPDATE SET
                      PlayCount = PlayCount + excluded.PlayCount,
                      TotalSeconds = TotalSeconds + excluded.TotalSeconds,
                      LastPlayedUtc = CASE WHEN excluded.LastPlayedUtc > LastPlayedUtc THEN excluded.LastPlayedUtc ELSE LastPlayedUtc END;
                    DROP TABLE History;
                    """;
                migrate.ExecuteNonQuery();
            }
        }
        catch { /* best-effort migration */ }
    }

    /// <summary>
    /// Finds existing track by Title+Artist or creates a new one.
    /// Increments play count and updates last played time. Returns track Id.
    /// </summary>
    public async Task<long> TouchTrackAsync(string title, string artist, DateTime playedUtc)
    {
        await using var db = new SqliteConnection(ConnectionString);
        await db.OpenAsync();
        var time = playedUtc.ToUniversalTime().ToString("O");

        await using var find = db.CreateCommand();
        find.CommandText = "SELECT Id FROM Tracks WHERE Title=$title AND Artist=$artist LIMIT 1";
        find.Parameters.AddWithValue("$title", title);
        find.Parameters.AddWithValue("$artist", artist);
        var existing = await find.ExecuteScalarAsync();

        if (existing is long id || (existing is not null && long.TryParse(existing.ToString(), out id)))
        {
            await using var upd = db.CreateCommand();
            upd.CommandText = """
                UPDATE Tracks
                SET PlayCount = PlayCount + 1,
                    LastPlayedUtc = $time
                WHERE Id = $id
                """;
            upd.Parameters.AddWithValue("$time", time);
            upd.Parameters.AddWithValue("$id", id);
            await upd.ExecuteNonQueryAsync();
            return id;
        }

        await using var ins = db.CreateCommand();
        ins.CommandText = """
            INSERT INTO Tracks(Title, Artist, PlayCount, TotalSeconds, FirstPlayedUtc, LastPlayedUtc)
            VALUES($title, $artist, 1, 0, $time, $time);
            SELECT last_insert_rowid();
            """;
        ins.Parameters.AddWithValue("$title", title);
        ins.Parameters.AddWithValue("$artist", artist);
        ins.Parameters.AddWithValue("$time", time);
        return (long)(await ins.ExecuteScalarAsync() ?? 0L);
    }

    public async Task AddListeningTimeAsync(long trackId, int seconds)
    {
        if (trackId <= 0 || seconds <= 0) return;
        await using var db = new SqliteConnection(ConnectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE Tracks SET TotalSeconds = TotalSeconds + $seconds WHERE Id = $id";
        cmd.Parameters.AddWithValue("$seconds", seconds);
        cmd.Parameters.AddWithValue("$id", trackId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<HistoryItem>> GetTracksAsync(string orderBy = "recent", int limit = 500)
    {
        var result = new List<HistoryItem>();
        await using var db = new SqliteConnection(ConnectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();

        var order = orderBy switch
        {
            "plays" => "PlayCount DESC, TotalSeconds DESC, LastPlayedUtc DESC",
            "time" => "TotalSeconds DESC, PlayCount DESC, LastPlayedUtc DESC",
            _ => "LastPlayedUtc DESC"
        };

        cmd.CommandText = $"""
            SELECT Title, Artist, PlayCount, TotalSeconds, LastPlayedUtc
            FROM Tracks
            ORDER BY {order}
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 2000));
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new HistoryItem
            {
                Title = reader.GetString(0),
                Artist = reader.GetString(1),
                PlayCount = reader.GetInt32(2),
                TotalSeconds = reader.GetInt64(3),
                LastPlayedUtc = DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime()
            });
        }
        return result;
    }

    public async Task<(int tracks, int plays, long totalSeconds)> GetSummaryAsync()
    {
        await using var db = new SqliteConnection(ConnectionString);
        await db.OpenAsync();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(PlayCount),0), COALESCE(SUM(TotalSeconds),0) FROM Tracks";
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt64(2));
        return (0, 0, 0);
    }
}
