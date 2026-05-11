using Microsoft.AspNetCore.Http;
using Quartz;
using QuartzDashboard.Internal;
using QuartzDashboard.Abstractions;

namespace QuartzDashboard.Handlers;

/// <summary>
/// Handles the /api/health endpoint for scheduler health checks.
/// </summary>
internal static class HealthHandlers
{
    public static async Task<IResult> GetHealth(IScheduler sched, IFireHistoryStore historyStore)
    {
        var meta = await sched.GetMetaData();
        var recent = historyStore.GetRecent(500).ToList();
        var failures = recent.Count(r => !r.Success);
        var successRate = recent.Count > 0
            ? Math.Round((double)(recent.Count - failures) / recent.Count * 100, 1)
            : 100.0;
        var status = sched is { IsStarted: true, InStandbyMode: false }
            ? successRate >= 95 ? "healthy" : successRate >= 80 ? "degraded" : "failing"
            : "degraded";

        return Results.Ok(new
        {
            status,
            scheduler = new
            {
                name = meta.SchedulerName,
                instanceId = meta.SchedulerInstanceId,
                isStarted = sched.IsStarted,
                isStandby = sched.InStandbyMode,
                version = meta.Version,
                uptime = meta.RunningSince.HasValue
                    ? (DateTimeOffset.UtcNow - meta.RunningSince.Value).ToString()
                    : null,
            },
            stats = new
            {
                totalExecutions = meta.NumberOfJobsExecuted,
                historyCount = historyStore.Count,
                threadPoolSize = meta.ThreadPoolSize,
                successRate,
                recentFailures = failures,
            }
        });
    }
}
