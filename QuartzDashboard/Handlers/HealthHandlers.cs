using Microsoft.AspNetCore.Http;
using Quartz;
using QuartzDashboard.Internal;

namespace QuartzDashboard.Handlers;

/// <summary>
/// Handles the /api/health endpoint for scheduler health checks.
/// </summary>
internal static class HealthHandlers
{
    public static async Task<IResult> GetHealth(IScheduler sched, IFireHistoryStore historyStore)
    {
        var meta = await sched.GetMetaData();
        return Results.Ok(new
        {
            status = sched.IsStarted && !sched.InStandbyMode ? "healthy" : "degraded",
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
            }
        });
    }
}
