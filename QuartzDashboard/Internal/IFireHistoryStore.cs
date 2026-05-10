using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace QuartzDashboard.Internal;

/// <summary>
/// Defines storage operations for persisting and querying recent Quartz job execution history.
/// </summary>
public interface IFireHistoryStore
{
    /// <summary>
    /// Records a completed job execution.
    /// </summary>
    /// <param name="jobKey">The fully qualified Quartz job key in <c>group.name</c> format.</param>
    /// <param name="triggerKey">The fully qualified Quartz trigger key in <c>group.name</c> format.</param>
    /// <param name="fireTime">The UTC time when the execution started.</param>
    /// <param name="duration">The total execution duration.</param>
    /// <param name="success"><see langword="true"/> when the execution completed without an exception.</param>
    /// <param name="refireCount">The Quartz refire count for the execution.</param>
    /// <param name="exceptionMessage">The exception message captured for a failed execution, if any.</param>
    /// <param name="exceptionType">The exception type captured for a failed execution, if any.</param>
    void RecordFire(string jobKey, string triggerKey, DateTimeOffset fireTime, TimeSpan duration, bool success, int refireCount = 0, string? exceptionMessage = null, string? exceptionType = null);

    /// <summary>
    /// Gets the number of records currently stored.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets recent execution records in reverse chronological order.
    /// </summary>
    /// <param name="count">The maximum number of records to return.</param>
    /// <param name="offset">The number of most recent records to skip.</param>
    /// <returns>A sequence of recent <see cref="FireRecord"/> entries.</returns>
    IEnumerable<FireRecord> GetRecent(int count, int offset = 0);

    /// <summary>
    /// Removes all stored execution records.
    /// </summary>
    void Clear();

    /// <summary>
    /// Occurs when a new execution record is stored.
    /// </summary>
    event Action<FireRecord>? OnFireRecorded;
}

/// <summary>
/// Represents a single recorded Quartz job execution.
/// </summary>
public sealed record FireRecord
{
    /// <summary>
    /// Gets or sets the fully qualified Quartz job key in <c>group.name</c> format.
    /// </summary>
    public string JobKey { get; set; } = "";

    /// <summary>
    /// Gets or sets the fully qualified Quartz trigger key in <c>group.name</c> format.
    /// </summary>
    public string TriggerKey { get; set; } = "";

    /// <summary>
    /// Gets or sets the UTC time when the execution started.
    /// </summary>
    public DateTimeOffset FireTime { get; set; }

    /// <summary>
    /// Gets or sets the total duration of the execution.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the execution completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the Quartz refire count for the execution.
    /// </summary>
    public int RefireCount { get; set; }

    /// <summary>
    /// Gets or sets the captured exception message for a failed execution, if available.
    /// </summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>
    /// Gets or sets the captured exception type for a failed execution, if available.
    /// </summary>
    public string? ExceptionType { get; set; }
}

/// <summary>
/// In-memory fire history store with optional TTL pruning.
/// Records are lost on app restart unless PersistHistoryPath is configured.
/// </summary>
internal sealed class InMemoryFireHistoryStore : IFireHistoryStore
{
    private readonly ConcurrentQueue<FireRecord> _queue = new();
    private readonly int _maxRecords;
    private readonly int _retentionHours;

    public InMemoryFireHistoryStore(int maxRecords = 500, int retentionHours = 24)
    {
        _maxRecords = maxRecords;
        _retentionHours = retentionHours;
    }

    public int Count => _queue.Count;

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
            ExceptionType = exceptionType
        };
        _queue.Enqueue(record);
        OnFireRecorded?.Invoke(record);

        // Prune by TTL first
        if (_retentionHours > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-_retentionHours);
            while (_queue.TryPeek(out var oldest) && oldest.FireTime < cutoff)
                _queue.TryDequeue(out _);
        }

        // Prune by max count
        while (_queue.Count > _maxRecords && _queue.TryDequeue(out _)) { }
    }

    public IEnumerable<FireRecord> GetRecent(int count, int offset = 0)
    {
        var all = _queue.ToArray();
        // Apply TTL filter on reads too
        if (_retentionHours > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-_retentionHours);
            all = all.Where(r => r.FireTime >= cutoff).ToArray();
        }
        return ((IEnumerable<FireRecord>)all).Reverse().Skip(offset).Take(count);
    }

    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }

    /// <summary>Seeds records from a previous session (used by FileFireHistoryStore on startup).</summary>
    internal void Seed(IEnumerable<FireRecord> records)
    {
        foreach (var r in records)
            _queue.Enqueue(r);
    }
}

/// <summary>
/// File-backed fire history store. Reads history from disk on startup, persists after every write.
/// Uses System.Text.Json — no extra NuGet dependencies required.
/// </summary>
internal sealed class FileFireHistoryStore : IFireHistoryStore
{
    private readonly InMemoryFireHistoryStore _inner;
    private readonly string _filePath;
    private readonly ILogger<FileFireHistoryStore> _logger;
    private readonly object _writeLock = new();

    public FileFireHistoryStore(string filePath, ILogger<FileFireHistoryStore> logger, int maxRecords = 500, int retentionHours = 24)
    {
        _filePath = filePath;
        _logger = logger;
        _inner = new InMemoryFireHistoryStore(maxRecords, retentionHours);
        _inner.OnFireRecorded += _ => PersistAsync();

        // Load existing records from disk
        LoadFromDisk();
    }

    public int Count => _inner.Count;
    public event Action<FireRecord>? OnFireRecorded
    {
        add => _inner.OnFireRecorded += value;
        remove => _inner.OnFireRecorded -= value;
    }

    public void RecordFire(string jobKey, string triggerKey, DateTimeOffset fireTime, TimeSpan duration, bool success, int refireCount = 0, string? exceptionMessage = null, string? exceptionType = null)
        => _inner.RecordFire(jobKey, triggerKey, fireTime, duration, success, refireCount, exceptionMessage, exceptionType);

    public IEnumerable<FireRecord> GetRecent(int count, int offset = 0)
        => _inner.GetRecent(count, offset);

    public void Clear()
    {
        _inner.Clear();
        PersistAsync();
    }

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var records = JsonSerializer.Deserialize<List<FireRecord>>(json);
            if (records?.Count > 0)
                _inner.Seed(records);
        }
        catch { /* corrupt or missing file — start fresh */ }
    }

    private void PersistAsync()
    {
        // Fire-and-forget write on thread pool so we never block the caller
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                lock (_writeLock)
                {
                    var records = _inner.GetRecent(int.MaxValue).ToList();
                    var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = false });
                    var dir = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(_filePath, json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist fire history to {Path}", _filePath);
            }
        });
    }
}
