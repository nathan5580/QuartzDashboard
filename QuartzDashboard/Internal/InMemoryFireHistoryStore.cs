using System.Collections.Concurrent;
using QuartzDashboard.Abstractions;

namespace QuartzDashboard.Internal;

/// <summary>
/// In-memory fire history store with optional TTL pruning.
/// Records are lost on app restart unless a persistent store is configured.
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

        if (_retentionHours > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-_retentionHours);
            while (_queue.TryPeek(out var oldest) && oldest.FireTime < cutoff)
                _queue.TryDequeue(out _);
        }

        while (_queue.Count > _maxRecords && _queue.TryDequeue(out _)) { }
    }

    public IEnumerable<FireRecord> GetRecent(int count, int offset = 0)
    {
        var all = _queue.ToArray();
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
