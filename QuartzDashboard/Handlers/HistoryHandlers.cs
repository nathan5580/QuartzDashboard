using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using QuartzDashboard.Internal;
using QuartzDashboard.Services;

namespace QuartzDashboard.Handlers;

/// <summary>
/// Handlers for fire history, stats, and timeline endpoints.
/// </summary>
internal static class HistoryHandlers
{
    public static IResult GetFireHistory(HttpContext ctx)
    {
        var offset = int.TryParse(ctx.Request.Query["offset"], out var o) ? o : 0;
        var limit = int.TryParse(ctx.Request.Query["limit"], out var l) ? Math.Min(l, 200) : 50;
        var job = ctx.Request.Query["job"].FirstOrDefault();

        var store = ctx.RequestServices.GetRequiredService<IFireHistoryStore>();
        var filtered = store.GetRecent(int.MaxValue, 0);

        if (!string.IsNullOrWhiteSpace(job))
            filtered = filtered.Where(f => string.Equals(f.JobKey, job, StringComparison.OrdinalIgnoreCase));

        var filteredList = filtered.ToList();
        var records = filteredList.Skip(offset).Take(limit).Select(f => new
        {
            jobKey = f.JobKey,
            triggerKey = f.TriggerKey,
            fireTime = f.FireTime,
            duration = f.Duration.TotalMilliseconds,
            success = f.Success,
            refireCount = f.RefireCount,
            relativeTime = (DateTimeOffset.UtcNow - f.FireTime).TotalSeconds,
        }).ToList();

        return Results.Ok(new { data = records, total = filteredList.Count, offset, limit });
    }

    public static IResult GetTimeline(HttpContext ctx)
    {
        var store = ctx.RequestServices.GetRequiredService<IFireHistoryStore>();
        var events = store.GetRecent(100).Select(f => new
        {
            jobKey = f.JobKey,
            triggerKey = f.TriggerKey,
            fireTime = f.FireTime,
            duration = f.Duration.TotalMilliseconds,
            success = f.Success,
            relativeTime = (DateTimeOffset.UtcNow - f.FireTime).TotalSeconds,
        }).ToList();

        return Results.Ok(events);
    }

    public static async Task<IResult> GetStats(IScheduler sched, ExecutionBucketService bucketService)
    {
        var meta = await sched.GetMetaData();
        var buckets = bucketService.GetBuckets()
            .OrderBy(b => b.Timestamp)
            .Select(b =>
            {
                var errorRate = b.ExecutionCount > 0
                    ? Math.Round((double)b.ErrorCount / b.ExecutionCount * 100, 1)
                    : 0;
                return new
                {
                    Minute = b.Timestamp.ToString("o"),
                    Label = b.Timestamp.ToString("HH:mm"),
                    Count = b.ExecutionCount,
                    AvgDurationMs = b.ExecutionCount > 0
                        ? Math.Round(b.TotalDurationMs / b.ExecutionCount, 1)
                        : 0,
                    ErrorRate = errorRate,
                    SuccessRate = Math.Round(100.0 - errorRate, 1),
                };
            }).ToList();

        return Results.Ok(new
        {
            TotalExecutions = meta.NumberOfJobsExecuted,
            UptimeMinutes = meta.RunningSince.HasValue
                ? Math.Round((DateTimeOffset.UtcNow - meta.RunningSince.Value).TotalMinutes, 1)
                : 0,
            SchedulerVersion = meta.Version ?? "?",
            ThreadPoolSize = meta.ThreadPoolSize,
            ExecutionBuckets = buckets,
            ExecutionRate = buckets.Count >= 2
                ? Math.Round(buckets.TakeLast(5).Average(b => b.Count), 1)
                : 0,
            AverageDurationMs = buckets.Count > 0
                ? Math.Round(buckets.Average(b => b.AvgDurationMs), 1)
                : 0,
        });
    }

    public static IResult GetHistoryBuckets(HttpContext ctx)
    {
        var store = ctx.RequestServices.GetRequiredService<IFireHistoryStore>();
        var records = store.GetRecent(500, 0);

        var buckets = records
            .GroupBy(r => new {
                r.FireTime.Year,
                r.FireTime.Month,
                r.FireTime.Day,
                r.FireTime.Hour,
                r.FireTime.Minute
            })
            .Select(g =>
            {
                var count = g.Count();
                var errorCount = g.Count(r => !r.Success);
                var totalMs = g.Sum(r => r.Duration.TotalMilliseconds);
                var errorRate = count > 0 ? Math.Round((double)errorCount / count * 100, 1) : 0.0;
                return new
                {
                    minute = new DateTimeOffset(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Key.Minute, 0, TimeSpan.Zero).ToString("o"),
                    count,
                    avgDurationMs = count > 0 ? Math.Round(totalMs / count, 1) : 0.0,
                    errorRate,
                    successRate = Math.Round(100.0 - errorRate, 1)
                };
            })
            .OrderBy(b => b.minute)
            .ToList();

        return Results.Ok(buckets);
    }
}
