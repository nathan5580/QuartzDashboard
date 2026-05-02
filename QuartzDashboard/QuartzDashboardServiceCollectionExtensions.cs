using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using QuartzDashboard.Internal;

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
        services.AddSingleton<DashboardEventBus>();

        if (options.UseSignalR)
        {
            services.AddSignalR();
            services.AddSingleton<IHostedService, DashboardSignalRBridge>();
        }

        return services;
    }

    /// <summary>
    /// Registers a Quartz job listener that records fire history for the dashboard.
    /// Called AFTER <c>AddQuartz()</c>.
    /// </summary>
    public static IServiceCollection AddQuartzDashboardHistory(this IServiceCollection services)
    {
        services.AddSingleton<IHostedService, DashboardListenerAttacher>();
        services.AddSingleton<ISchedulerListener, DashboardSchedulerListener>();
        return services;
    }
}

internal sealed class DashboardListenerAttacher(
    ISchedulerFactory schedulerFactory,
    DashboardEventBus eventBus,
    ISchedulerListener schedulerListener,
    ILogger<DashboardListenerAttacher> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var scheduler = await schedulerFactory.GetScheduler(ct);
            scheduler.ListenerManager.AddJobListener(new DashboardJobListener(eventBus));
            scheduler.ListenerManager.AddSchedulerListener(schedulerListener);
            logger.LogDebug("QuartzDashboard listeners attached");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to attach QuartzDashboard listeners");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

internal sealed class DashboardJobListener(DashboardEventBus eventBus) : IJobListener
{
    public string Name => "QuartzDashboardListener";
    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken ct) => Task.CompletedTask;
    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken ct) => Task.CompletedTask;

    public Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken ct)
    {
        var jobKey = $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}";
        var triggerKey = $"{context.Trigger.Key.Group}.{context.Trigger.Key.Name}";
        var duration = DateTimeOffset.UtcNow - context.FireTimeUtc;
        var success = jobException == null;

        // Update in-memory stats
        QuartzDashboardApplicationBuilderExtensions.RecordFire(jobKey, triggerKey, context.FireTimeUtc, duration);

        // Publish to event bus for SignalR
        eventBus.Publish(new JobExecutedEvent(jobKey, triggerKey, duration, success, context.FireTimeUtc));
        return Task.CompletedTask;
    }
}
