using System.Collections.Concurrent;

namespace QuartzDashboard.Internal;

/// <summary>
/// Abstraction for storing and retrieving fire history records.
/// Default implementation uses an in-memory ConcurrentQueue.
/// </summary>
public interface IFireHistoryStore
{
    void RecordFire(string jobKey, string triggerKey, DateTimeOffset fireTime, TimeSpan duration, bool success);
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
}

/// <summary>
/// In-memory fire history store using a ConcurrentQueue.
/// Records are lost on app restart. Max records configurable via QuartzDashboardOptions.MaxFireHistory.
/// </summary>
internal sealed class InMemoryFireHistoryStore(int maxRecords) : IFireHistoryStore
{
    private readonly ConcurrentQueue<FireRecord> _queue = new();
    private readonly int _maxRecords = maxRecords;

    public InMemoryFireHistoryStore() : this(100) { }

    public int Count => _queue.Count;

    public event Action<FireRecord>? OnFireRecorded;

    public void RecordFire(string jobKey, string triggerKey, DateTimeOffset fireTime, TimeSpan duration, bool success)
    {
        var record = new FireRecord
        {
            JobKey = jobKey,
            TriggerKey = triggerKey,
            FireTime = fireTime,
            Duration = duration,
            Success = success
        };
        _queue.Enqueue(record);
        OnFireRecorded?.Invoke(record);
        while (_queue.Count > _maxRecords && _queue.TryDequeue(out _)) { }
    }

    public IEnumerable<FireRecord> GetRecent(int count, int offset = 0)
    {
        return _queue.Reverse().Skip(offset).Take(count);
    }

    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }
}
