using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using QuartzDashboard.Internal;
using QuartzDashboard.Abstractions;
using QuartzDashboard.Models;
using QuartzDashboard.Services;

namespace QuartzDashboard.Handlers;

/// <summary>
/// Handlers for fire history, stats, and timeline endpoints.
/// </summary>
internal static class HistoryHandlers
{
    public static IResult GetFireHistory(HttpContext ctx)
    {
        var offset = int.TryParse(ctx.Request.Query["offset"], out var o) ? Math.Max(0, o) : 0;
        var limit = int.TryParse(ctx.Request.Query["limit"], out var l) ? Math.Clamp(l, 1, 200) : 50;
        var job = ctx.Request.Query["job"].FirstOrDefault();

        var store = ctx.RequestServices.GetRequiredService<IFireHistoryStore>();

        var total = store.CountFiltered(job);
        var records = store.GetRecent(limit, offset, job).Select(ToDto).ToList();

        return Results.Ok(new PagedResponse<FireRecordDto>(records, total, offset, limit));
    }

    public static IResult GetTimeline(HttpContext ctx)
    {
        var store = ctx.RequestServices.GetRequiredService<IFireHistoryStore>();
        var events = store.GetRecent(500).Select(ToDto).ToList();
        return Results.Ok(events);
    }

    private static FireRecordDto ToDto(FireRecord f) => new(
        JobKey: f.JobKey,
        TriggerKey: f.TriggerKey,
        FireTime: f.FireTime,
        Duration: f.Duration.TotalMilliseconds,
        Success: f.Success,
        RefireCount: f.RefireCount,
        RelativeTime: (DateTimeOffset.UtcNow - f.FireTime).TotalSeconds,
        ExceptionMessage: f.ExceptionMessage,
        ExceptionType: f.ExceptionType);

    public static IResult GetHeatmap(HttpContext ctx)
    {
        var store = ctx.RequestServices.GetRequiredService<IFireHistoryStore>();
        var records = store.GetRecent(500, 0);

        var grid = new int[7, 24];
        var successGrid = new int[7, 24];

        foreach (var r in records)
        {
            var local = r.FireTime.LocalDateTime;
            var dow = (int)local.DayOfWeek;
            var hour = local.Hour;
            grid[dow, hour]++;
            if (r.Success) successGrid[dow, hour]++;
        }

        var cells = new List<object>();
        for (int d = 0; d < 7; d++)
            for (int h = 0; h < 24; h++)
                cells.Add(new
                {
                    day = d,
                    hour = h,
                    count = grid[d, h],
                    successRate = grid[d, h] > 0 ? Math.Round((double)successGrid[d, h] / grid[d, h] * 100, 1) : 100.0
                });

        return Results.Ok(cells);
    }

    public static async Task<IResult> GetStats(IScheduler sched, ExecutionBucketService bucketService, IFireHistoryStore historyStore)
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
            Percentiles = ComputePercentiles(historyStore),
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

    private static object ComputePercentiles(IFireHistoryStore store)
    {
        var all = store.GetRecent(1000, 0).ToList();
        if (all.Count == 0) return new { p50 = 0.0, p95 = 0.0, p99 = 0.0, count = 0, perJob = Array.Empty<object>() };

        var durations = all.Select(r => r.Duration.TotalMilliseconds).OrderBy(d => d).ToList();
        double Percentile(List<double> sorted, double p)
        {
            var idx = (p / 100.0) * (sorted.Count - 1);
            var lower = (int)Math.Floor(idx);
            var upper = (int)Math.Ceiling(idx);
            if (lower == upper) return Math.Round(sorted[lower], 1);
            return Math.Round(sorted[lower] + (idx - lower) * (sorted[upper] - sorted[lower]), 1);
        }

        var perJob = all
            .GroupBy(r => r.JobKey)
            .Select(g =>
            {
                var d = g.Select(r => r.Duration.TotalMilliseconds).OrderBy(x => x).ToList();
                var successCount = g.Count(r => r.Success);
                return new
                {
                    jobKey = g.Key,
                    count = g.Count(),
                    successRate = g.Count() > 0 ? Math.Round((double)successCount / g.Count() * 100, 1) : 100.0,
                    p50 = Percentile(d, 50),
                    p95 = Percentile(d, 95),
                    p99 = Percentile(d, 99),
                };
            })
            .OrderBy(j => j.jobKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new
        {
            p50 = Percentile(durations, 50),
            p95 = Percentile(durations, 95),
            p99 = Percentile(durations, 99),
            count = all.Count,
            perJob,
        };
    }
}
