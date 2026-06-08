using Microsoft.Extensions.Diagnostics.HealthChecks;
using Quartz;
using QuartzDashboard.Abstractions;

namespace QuartzDashboard.Middleware;

internal sealed class DashboardHealthCheck : IHealthCheck
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IFireHistoryStore _historyStore;

    public DashboardHealthCheck(ISchedulerFactory schedulerFactory, IFireHistoryStore historyStore)
    {
        _schedulerFactory = schedulerFactory;
        _historyStore = historyStore;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);
        var meta = await scheduler.GetMetaData(ct);

        var recent = _historyStore.GetRecent(500).ToList();
        var failures = recent.Count(r => !r.Success);
        var successRate = recent.Count > 0
            ? Math.Round((double)(recent.Count - failures) / recent.Count * 100, 1)
            : 100.0;

        var status = scheduler is { IsStarted: true, InStandbyMode: false }
            ? successRate >= 95 ? HealthStatus.Healthy
            : successRate >= 80 ? HealthStatus.Degraded
            : HealthStatus.Unhealthy
            : HealthStatus.Degraded;

        var data = new Dictionary<string, object>
        {
            ["scheduler.name"] = meta.SchedulerName,
            ["scheduler.isStarted"] = scheduler.IsStarted,
            ["scheduler.isStandby"] = scheduler.InStandbyMode,
            ["scheduler.version"] = meta.Version ?? "?",
            ["stats.totalExecutions"] = meta.NumberOfJobsExecuted,
            ["stats.historyCount"] = _historyStore.Count,
            ["stats.threadPoolSize"] = meta.ThreadPoolSize,
            ["stats.successRate"] = successRate,
            ["stats.recentFailures"] = failures,
        };

        if (meta.RunningSince.HasValue)
            data["scheduler.uptime"] = (DateTimeOffset.UtcNow - meta.RunningSince.Value).ToString();

        return new HealthCheckResult(status, $"Scheduler: {meta.SchedulerName}, success rate: {successRate}%",
            data: data);
    }
}
