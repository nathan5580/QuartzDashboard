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

        var store = ctx.RequestServices.GetRequiredService<IFireHistoryStore>();
        var records = store.GetRecent(limit, offset).Select(f => new
        {
            jobKey = f.JobKey,
            triggerKey = f.TriggerKey,
            fireTime = f.FireTime,
            duration = f.Duration.TotalMilliseconds,
            success = f.Success,
            relativeTime = (DateTimeOffset.UtcNow - f.FireTime).TotalSeconds,
        }).ToList();

        return Results.Ok(new { data = records, total = store.Count, offset, limit });
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
            .Select(b => new
            {
                Minute = b.Timestamp.ToString("HH:mm"),
                Count = b.ExecutionCount,
                AvgDurationMs = b.ExecutionCount > 0
                    ? Math.Round(b.TotalDurationMs / b.ExecutionCount, 1)
                    : 0,
                ErrorRate = b.ExecutionCount > 0
                    ? Math.Round((double)b.ErrorCount / b.ExecutionCount * 100, 1)
                    : 0,
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
}
