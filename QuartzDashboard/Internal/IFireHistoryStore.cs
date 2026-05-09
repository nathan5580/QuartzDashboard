using System.Collections.Concurrent;
using System.Text.Json;

namespace QuartzDashboard.Internal;

/// <summary>
/// Abstraction for storing and retrieving fire history records.
/// Default implementation uses an in-memory ConcurrentQueue.
/// </summary>
public interface IFireHistoryStore
{
    void RecordFire(string jobKey, string triggerKey, DateTimeOffset fireTime, TimeSpan duration, bool success, int refireCount = 0, string? exceptionMessage = null, string? exceptionType = null);
    int Count { get; }
    IEnumerable<FireRecord> GetRecent(int count, int offset = 0);
    void Clear();
    event Action<FireRecord>? OnFireRecorded;
}

/// <summary>
/// A single fire execution record.
/// </summary>
public sealed record FireRecord
{
    public string JobKey { get; set; } = "";
    public string TriggerKey { get; set; } = "";
    public DateTimeOffset FireTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public int RefireCount { get; set; }
    public string? ExceptionMessage { get; set; }
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
        return all.Reverse().Skip(offset).Take(count);
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
    private readonly object _writeLock = new();

    public FileFireHistoryStore(string filePath, int maxRecords = 500, int retentionHours = 24)
    {
        _filePath = filePath;
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
            catch { /* best-effort persistence */ }
        });
    }
}
