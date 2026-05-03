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

    /// <summary>
    /// Encodes a DateTimeOffset into a compact long (YYYYMMDDHHMM).
    /// Example: 2026-05-03T10:30:00 -> 202605031030
    /// </summary>
    private static long EncodeMinute(DateTimeOffset dt) =>
        dt.Year * 100000000L
        + dt.Month * 1000000L
        + dt.Day * 10000L
        + dt.Hour * 100L
        + dt.Minute;

    /// <summary>
    /// Decodes a compact long back to DateTimeOffset.
    /// </summary>
    private static DateTimeOffset DecodeMinute(long encoded) =>
        new(
            year: (int)(encoded / 100000000),
            month: (int)(encoded / 1000000 % 100),
            day: (int)(encoded / 10000 % 100),
            hour: (int)(encoded / 100 % 100),
            minute: (int)(encoded % 100),
            second: 0,
            offset: TimeSpan.Zero
        );

    public void Record(TimeSpan duration, bool success)
    {
        var now = DateTimeOffset.UtcNow;
        var minute = EncodeMinute(now);

        var bucket = _buckets.GetOrAdd(minute, _ => new Bucket());
        Interlocked.Increment(ref bucket.ExecutionCount);
        Interlocked.Add(ref bucket.TotalDurationMs, (long)duration.TotalMilliseconds);

        if (!success)
            Interlocked.Increment(ref bucket.ErrorCount);

        // Prune old buckets (best-effort, not every call)
        if (_buckets.Count > MaxBuckets)
        {
            var cutoff = minute - MaxBuckets;
            foreach (var key in _buckets.Keys)
            {
                if (key < cutoff)
                    _buckets.TryRemove(key, out _);
            }
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
                Timestamp = DecodeMinute(kvp.Key),
            });
    }

    public void Clear()
    {
        _buckets.Clear();
    }
}
