using System.Collections.Concurrent;
using QuartzDashboard.Models;

namespace QuartzDashboard.Services;

/// <summary>
/// Thread-safe execution statistics aggregator.
/// Buckets executions by minute with lock-free concurrent access.
/// </summary>
internal sealed class ExecutionBucketService
{
    private const int MaxBuckets = 120;
    private readonly ConcurrentDictionary<long, Bucket> _buckets = new();

    internal sealed class Bucket
    {
        public int ExecutionCount;
        public long TotalDurationMs;
        public int ErrorCount;
    }

    // Unix-epoch minutes are contiguous, so cutoff arithmetic (currentMinute - MaxBuckets)
    // works correctly across hour/day/month/year rollovers.
    private static long ToUnixMinute(DateTimeOffset dt) =>
        dt.ToUnixTimeSeconds() / 60;

    private static DateTimeOffset FromUnixMinute(long minute) =>
        DateTimeOffset.FromUnixTimeSeconds(minute * 60);

    public void Record(TimeSpan duration, bool success)
    {
        var minute = ToUnixMinute(DateTimeOffset.UtcNow);

        var bucket = _buckets.GetOrAdd(minute, _ => new Bucket());
        Interlocked.Increment(ref bucket.ExecutionCount);
        Interlocked.Add(ref bucket.TotalDurationMs, (long)duration.TotalMilliseconds);

        if (!success)
            Interlocked.Increment(ref bucket.ErrorCount);

        // Time-based prune: drop anything older than MaxBuckets minutes.
        // Runs every call but the dictionary stays small, so the scan is cheap.
        var cutoff = minute - MaxBuckets;
        foreach (var key in _buckets.Keys)
        {
            if (key < cutoff)
                _buckets.TryRemove(key, out _);
        }
    }

    public IEnumerable<ExecutionBucket> GetBuckets()
    {
        return _buckets
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new ExecutionBucket
            {
                ExecutionCount = kvp.Value.ExecutionCount,
                TotalDurationMs = kvp.Value.TotalDurationMs,
                ErrorCount = kvp.Value.ErrorCount,
                Timestamp = FromUnixMinute(kvp.Key),
            });
    }

    public void Clear()
    {
        _buckets.Clear();
    }
}
