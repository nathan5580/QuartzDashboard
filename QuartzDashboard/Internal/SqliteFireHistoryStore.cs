using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace QuartzDashboard.Internal;

internal sealed class SqliteFireHistoryStore : IFireHistoryStore, IDisposable
{
    private readonly string _connectionString;
    private readonly int _maxHistory;
    private readonly int? _retentionHours;
    private readonly ILogger<SqliteFireHistoryStore> _logger;
    private readonly object _syncRoot = new();

    public SqliteFireHistoryStore(string dbPath, int maxHistory, int? retentionHours, ILogger<SqliteFireHistoryStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);

        var fullPath = Path.GetFullPath(dbPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        }.ToString();
        _maxHistory = Math.Max(0, maxHistory);
        _retentionHours = retentionHours > 0 ? retentionHours : null;
        _logger = logger;

        InitializeDatabase();
        _logger.LogDebug("SQLite fire history store initialized at {Path}", fullPath);
    }

    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                using var conn = OpenConnection();
                PruneExpired(conn);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM fire_history;";
                return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }
    }

    public event Action<FireRecord>? OnFireRecorded;

    public void RecordFire(string jobKey, string triggerKey, DateTimeOffset fireTime, TimeSpan duration, bool success, int refireCount = 0, string? exceptionMessage = null, string? exceptionType = null)
    {
        var record = new FireRecord
        {
            JobKey = jobKey,
            TriggerKey = triggerKey,
            FireTime = fireTime,
            Duration = duration,
            Success = success,
            RefireCount = refireCount,
            ExceptionMessage = exceptionMessage,
            ExceptionType = exceptionType,
        };

        lock (_syncRoot)
        {
            using var conn = OpenConnection();
            using var tx = conn.BeginTransaction();

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO fire_history (
                        fire_time,
                        job_key,
                        trigger_key,
                        duration_ticks,
                        success,
                        refire_count,
                        exception_message,
                        exception_type
                    )
                    VALUES (
                        $fireTime,
                        $jobKey,
                        $triggerKey,
                        $durationTicks,
                        $success,
                        $refireCount,
                        $exceptionMessage,
                        $exceptionType
                    );
                    """;
                cmd.Parameters.AddWithValue("$fireTime", ToStorageValue(record.FireTime));
                cmd.Parameters.AddWithValue("$jobKey", record.JobKey);
                cmd.Parameters.AddWithValue("$triggerKey", record.TriggerKey);
                cmd.Parameters.AddWithValue("$durationTicks", record.Duration.Ticks);
                cmd.Parameters.AddWithValue("$success", record.Success ? 1 : 0);
                cmd.Parameters.AddWithValue("$refireCount", record.RefireCount);
                cmd.Parameters.AddWithValue("$exceptionMessage", (object?)record.ExceptionMessage ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$exceptionType", (object?)record.ExceptionType ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            PruneExpired(conn, tx);
            PruneMaxHistory(conn, tx);
            tx.Commit();
        }

        OnFireRecorded?.Invoke(record);
    }

    public IEnumerable<FireRecord> GetRecent(int count, int offset = 0)
    {
        if (count <= 0)
            return [];

        offset = Math.Max(0, offset);
        var records = new List<FireRecord>();

        lock (_syncRoot)
        {
            using var conn = OpenConnection();
            PruneExpired(conn);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT fire_time, job_key, trigger_key, duration_ticks, success, refire_count, exception_message, exception_type
                FROM fire_history
                ORDER BY fire_time DESC, id DESC
                LIMIT $count OFFSET $offset;
                """;
            cmd.Parameters.AddWithValue("$count", count);
            cmd.Parameters.AddWithValue("$offset", offset);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                records.Add(ReadRecord(reader));
        }

        return records;
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM fire_history;";
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
    }

    private void InitializeDatabase()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS fire_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                fire_time TEXT NOT NULL,
                job_key TEXT NOT NULL,
                trigger_key TEXT NOT NULL,
                duration_ticks INTEGER NOT NULL,
                success INTEGER NOT NULL,
                refire_count INTEGER NOT NULL,
                exception_message TEXT NULL,
                exception_type TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_fire_history_fire_time ON fire_history(fire_time DESC, id DESC);
            """;
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void PruneExpired(SqliteConnection conn, SqliteTransaction? tx = null)
    {
        if (_retentionHours is not > 0)
            return;

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM fire_history WHERE fire_time < $cutoff;";
        cmd.Parameters.AddWithValue("$cutoff", ToStorageValue(DateTimeOffset.UtcNow.AddHours(-_retentionHours.Value)));
        cmd.ExecuteNonQuery();
    }

    private void PruneMaxHistory(SqliteConnection conn, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            DELETE FROM fire_history
            WHERE id IN (
                SELECT id
                FROM fire_history
                ORDER BY fire_time DESC, id DESC
                LIMIT -1 OFFSET $maxHistory
            );
            """;
        cmd.Parameters.AddWithValue("$maxHistory", _maxHistory);
        cmd.ExecuteNonQuery();
    }

    private static FireRecord ReadRecord(SqliteDataReader reader)
    {
        return new FireRecord
        {
            FireTime = DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            JobKey = reader.GetString(1),
            TriggerKey = reader.GetString(2),
            Duration = TimeSpan.FromTicks(reader.GetInt64(3)),
            Success = reader.GetInt64(4) == 1,
            RefireCount = reader.GetInt32(5),
            ExceptionMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
            ExceptionType = reader.IsDBNull(7) ? null : reader.GetString(7),
        };
    }

    private static string ToStorageValue(DateTimeOffset value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
