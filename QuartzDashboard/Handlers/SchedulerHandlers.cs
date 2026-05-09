using Microsoft.AspNetCore.Http;
using Quartz;

namespace QuartzDashboard.Handlers;

/// <summary>
/// Handles scheduler status and lifecycle endpoints.
/// </summary>
internal static class SchedulerHandlers
{
    public static async Task<IResult> GetSchedulerInfo(IScheduler sched)
    {
        var meta = await sched.GetMetaData();
        var executing = await sched.GetCurrentlyExecutingJobs();
        return Results.Ok(new
        {
            Name = meta.SchedulerName,
            InstanceId = meta.SchedulerInstanceId,
            IsStarted = sched.IsStarted,
            IsShutdown = sched.IsShutdown,
            IsStandbyMode = sched.InStandbyMode,
            JobStoreType = meta.JobStoreType?.Name ?? "Unknown",
            ThreadPoolType = meta.ThreadPoolType?.Name ?? "Unknown",
            NumberOfJobsExecuted = meta.NumberOfJobsExecuted,
            Summary = meta.GetSummary(),
            RunningJobs = executing.Count,
            Version = meta.Version ?? "?",
            UpSince = meta.RunningSince,
            UpTime = meta.RunningSince.HasValue
                ? DateTimeOffset.UtcNow - meta.RunningSince.Value
                : TimeSpan.Zero,
            ThreadPoolSize = meta.ThreadPoolSize,
        });
    }

    public static async Task<IResult> StandbyScheduler(IScheduler sched, QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return Results.Forbid();
        if (!sched.InStandbyMode)
        {
            await sched.Standby();
            return Results.Ok(new { Status = "standby" });
        }
        return Results.Ok(new { Status = "already_standby" });
    }

    public static async Task<IResult> GetSchedulers(ISchedulerFactory factory)
    {
        var names = await factory.GetAllSchedulers();
        var schedulers = names.Select(s => new { name = s.SchedulerName, instanceId = s.SchedulerInstanceId, isStarted = s.IsStarted }).ToList();
        return Results.Ok(schedulers);
    }

    public static async Task<IResult> StartScheduler(IScheduler sched, QuartzDashboardOptions options)
    {
        if (options.ReadOnly) return Results.Forbid();
        if (!sched.IsStarted || sched.InStandbyMode)
        {
            await sched.Start();
            return Results.Ok(new { Status = "started" });
        }
        return Results.Ok(new { Status = "already_running" });
    }
}
