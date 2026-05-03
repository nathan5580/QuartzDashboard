using System.Collections.Concurrent;

namespace QuartzDashboard.Internal;

/// <summary>
/// In-memory ring buffer for execution log entries per job key.
/// Each job stores the last N log entries (configurable via QuartzDashboardOptions.MaxExecutionLogsPerJob).
/// </summary>
internal sealed class ExecutionLogBuffer(int maxEntriesPerJob)
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _logs = new();
    private readonly int _maxEntries = maxEntriesPerJob;

    public ExecutionLogBuffer() : this(50) { }

    public void Append(string jobKey, string message)
    {
        var queue = _logs.GetOrAdd(jobKey, _ => new ConcurrentQueue<string>());
        queue.Enqueue($"[{DateTimeOffset.UtcNow:HH:mm:ss.fff}] {message}");
        while (queue.Count > _maxEntries && queue.TryDequeue(out _)) { }
    }

    public IReadOnlyList<string> GetLogs(string jobKey, int count = 50)
    {
        if (!_logs.TryGetValue(jobKey, out var queue))
            return Array.Empty<string>();
        return queue.Reverse().Take(count).Reverse().ToList();
    }
}
