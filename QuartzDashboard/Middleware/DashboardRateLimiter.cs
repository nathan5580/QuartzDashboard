using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace QuartzDashboard.Middleware;

internal sealed class DashboardRateLimiter
{
    private readonly int _requestsPerMinute;
    private readonly int _burstSize;
    private readonly ILogger<DashboardRateLimiter> _logger;

    private readonly ConcurrentDictionary<string, SlidingWindow> _windows = new();

    public DashboardRateLimiter(int requestsPerMinute, int burstSize, ILogger<DashboardRateLimiter> logger)
    {
        _requestsPerMinute = Math.Max(1, requestsPerMinute);
        _burstSize = Math.Max(1, burstSize);
        _logger = logger;
    }

    public bool IsAllowed(HttpContext ctx)
    {
        var key = ctx.Connection.RemoteIpAddress?.ToString()
                  ?? ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                  ?? "unknown";

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var window = _windows.GetOrAdd(key, _ => new SlidingWindow());

        var bucket = window.GetBucket(now);
        if (bucket.Count >= _burstSize)
        {
            _logger.LogWarning("Rate limit burst exceeded for {Ip} at {Path}", key, ctx.Request.Path);
            return false;
        }

        var minuteCount = window.CountInWindow(now, 60);
        if (minuteCount >= _requestsPerMinute)
        {
            _logger.LogWarning("Rate limit per-minute exceeded for {Ip} at {Path}", key, ctx.Request.Path);
            return false;
        }

        Interlocked.Increment(ref bucket.Count);
        window.PruneOldBuckets(now, 120);
        return true;
    }

    private sealed class SlidingWindow
    {
        private readonly ConcurrentDictionary<long, Bucket> _buckets = new();

        public Bucket GetBucket(long second)
        {
            return _buckets.GetOrAdd(second, _ => new Bucket());
        }

        public int CountInWindow(long now, int seconds)
        {
            var cutoff = now - seconds;
            var count = 0;
            foreach (var kv in _buckets)
            {
                if (kv.Key >= cutoff)
                    count += kv.Value.Count;
            }
            return count;
        }

        public void PruneOldBuckets(long now, int maxAgeSeconds)
        {
            var cutoff = now - maxAgeSeconds;
            foreach (var key in _buckets.Keys)
            {
                if (key < cutoff)
                    _buckets.TryRemove(key, out _);
            }
        }
    }

    private sealed class Bucket
    {
        public int Count;
    }
}
