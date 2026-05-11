using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using QuartzDashboard.Abstractions;

namespace QuartzDashboard.Sqlite;

/// <summary>
/// SQLite-backed fire history store. Enables WAL for concurrent reads, indexes by
/// <c>job_key</c> for filtered queries, and throttles TTL-based pruning to once per minute
/// so dashboard polling does not issue a DELETE on every read.
/// </summary>
public sealed class SqliteFireHistoryStore : IFireHistoryStore, IDisposable
{
    private readonly string _connectionString;
    private readonly int _maxHistory;
    private readonly int? _retentionHours;
    private readonly ILogger<SqliteFireHistoryStore> _logger;
    private readonly object _writeLock = new();
    private long _lastPruneTicks;

    private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Initializes a new SQLite-backed fire history store, creating the database file and
    /// schema if they do not already exist.
    /// </summary>
    /// <param name="dbPath">Path to the SQLite database file. Created if missing.</param>
    /// <param name="maxHistory">Maximum number of records kept; older rows are pruned after each write.</param>
    /// <param name="retentionHours">When &gt; 0, rows older than this many hours are dropped on a one-per-minute schedule.</param>
    /// <param name="logger">Logger used to surface initialization and persistence errors.</param>
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
            using var conn = OpenConnection();
            MaybePruneExpired();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM fire_history;";
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
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

        lock (_writeLock)
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
        => GetRecent(count, offset, null);

    public IEnumerable<FireRecord> GetRecent(int count, int offset, string? jobKeyFilter)
    {
        if (count <= 0)
            return [];

        offset = Math.Max(0, offset);
        var records = new List<FireRecord>();

        using var conn = OpenConnection();
        MaybePruneExpired();

        using var cmd = conn.CreateCommand();
        if (string.IsNullOrWhiteSpace(jobKeyFilter))
        {
            cmd.CommandText = """
                SELECT fire_time, job_key, trigger_key, duration_ticks, success, refire_count, exception_message, exception_type
                FROM fire_history
                ORDER BY fire_time DESC, id DESC
                LIMIT $count OFFSET $offset;
                """;
        }
        else
        {
            cmd.CommandText = """
                SELECT fire_time, job_key, trigger_key, duration_ticks, success, refire_count, exception_message, exception_type
                FROM fire_history
                WHERE job_key = $jobKey COLLATE NOCASE
                ORDER BY fire_time DESC, id DESC
                LIMIT $count OFFSET $offset;
                """;
            cmd.Parameters.AddWithValue("$jobKey", jobKeyFilter);
        }
        cmd.Parameters.AddWithValue("$count", count);
        cmd.Parameters.AddWithValue("$offset", offset);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            records.Add(ReadRecord(reader));

        return records;
    }

    public int CountFiltered(string? jobKeyFilter)
    {
        if (string.IsNullOrWhiteSpace(jobKeyFilter))
            return Count;

        using var conn = OpenConnection();
        MaybePruneExpired();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM fire_history WHERE job_key = $jobKey COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("$jobKey", jobKeyFilter);
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public void Clear()
    {
        lock (_writeLock)
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

        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            pragma.ExecuteNonQuery();
        }

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
            CREATE INDEX IF NOT EXISTS idx_fire_history_job_key ON fire_history(job_key, fire_time DESC);
            """;
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void MaybePruneExpired()
    {
        if (_retentionHours is not > 0)
            return;

        var nowTicks = DateTime.UtcNow.Ticks;
        var lastTicks = Interlocked.Read(ref _lastPruneTicks);
        if (nowTicks - lastTicks < PruneInterval.Ticks)
            return;

        if (Interlocked.CompareExchange(ref _lastPruneTicks, nowTicks, lastTicks) != lastTicks)
            return;

        lock (_writeLock)
        {
            using var writeConn = OpenConnection();
            PruneExpired(writeConn);
        }
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
