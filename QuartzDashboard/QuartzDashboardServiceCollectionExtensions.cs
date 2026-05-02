using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

namespace QuartzDashboard;

/// <summary>
/// Extension methods for registering the Quartz Dashboard services.
/// Call <c>builder.Services.AddQuartzDashboard()</c> after <c>AddQuartz()</c>.
/// </summary>
public static class QuartzDashboardServiceCollectionExtensions
{
    /// <summary>
    /// Adds Quartz Dashboard services. Quartz must already be configured
    /// (<c>AddQuartz</c> and <c>AddQuartzHostedService</c>).
    /// </summary>
    public static IServiceCollection AddQuartzDashboard(this IServiceCollection services, Action<QuartzDashboardOptions>? configure = null)
    {
        var options = new QuartzDashboardOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        return services;
    }

    /// <summary>
    /// Registers a Quartz job listener that records fire history for the dashboard.
    /// Called AFTER <c>AddQuartz()</c>.
    /// </summary>
    public static IServiceCollection AddQuartzDashboardHistory(this IServiceCollection services)
    {
        services.AddSingleton<IHostedService, DashboardListenerAttacher>();
        return services;
    }
}

internal sealed class DashboardListenerAttacher(
    ISchedulerFactory schedulerFactory,
    ILogger<DashboardListenerAttacher> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var scheduler = await schedulerFactory.GetScheduler(ct);
            scheduler.ListenerManager.AddJobListener(new DashboardJobListener());
            logger.LogDebug("QuartzDashboard history listener attached");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to attach QuartzDashboard history listener");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

internal sealed class DashboardJobListener : Quartz.IJobListener
{
    public string Name => "QuartzDashboardListener";
    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken ct) => Task.CompletedTask;
    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken ct) => Task.CompletedTask;

    public Task JobWasExecuted(IJobExecutionContext context, Quartz.JobExecutionException? jobException, CancellationToken ct)
    {
        var jobKey = $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}";
        var triggerKey = $"{context.Trigger.Key.Group}.{context.Trigger.Key.Name}";
        QuartzDashboardApplicationBuilderExtensions.RecordFire(jobKey, triggerKey, context.FireTimeUtc, DateTimeOffset.UtcNow - context.FireTimeUtc);
        return Task.CompletedTask;
    }
}
