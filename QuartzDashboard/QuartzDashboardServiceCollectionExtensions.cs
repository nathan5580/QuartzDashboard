using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using QuartzDashboard.Internal;
using QuartzDashboard.Services;

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
        services.AddHttpClient();

        // Fire history store — file-backed if PersistHistoryPath is set, otherwise in-memory
        if (!string.IsNullOrWhiteSpace(options.PersistHistoryPath))
            services.AddSingleton<IFireHistoryStore>(_ => new FileFireHistoryStore(options.PersistHistoryPath, options.MaxFireHistory, options.HistoryRetentionHours));
        else
            services.AddSingleton<IFireHistoryStore>(_ => new InMemoryFireHistoryStore(options.MaxFireHistory, options.HistoryRetentionHours));

        // Execution log buffer
        services.AddSingleton(_ => new ExecutionLogBuffer(options.MaxExecutionLogsPerJob));

        // Execution bucket service (thread-safe performance stats)
        services.AddSingleton<ExecutionBucketService>();

        // Event bus
        services.AddSingleton<DashboardEventBus>();

        if (options.UseSignalR)
        {
            services.AddSignalR();
            services.AddSingleton<IHostedService, DashboardSignalRBridge>();
        }

        // History listener is always registered — timelines and graphs work out of the box
        services.AddSingleton<IHostedService, DashboardListenerAttacher>();
        services.AddSingleton<ISchedulerListener, DashboardSchedulerListener>();

        return services;
    }

    /// <summary>
    /// No-op — history is now registered automatically by <c>AddQuartzDashboard()</c>.
    /// Kept for backwards compatibility.
    /// </summary>
    [Obsolete("History is registered automatically by AddQuartzDashboard(). This call is no longer needed.")]
    public static IServiceCollection AddQuartzDashboardHistory(this IServiceCollection services) => services;
}

internal sealed class DashboardListenerAttacher(
    ISchedulerFactory schedulerFactory,
    DashboardEventBus eventBus,
    ISchedulerListener schedulerListener,
    IFireHistoryStore fireHistoryStore,
    ExecutionLogBuffer? logBuffer,
    ExecutionBucketService? bucketService,
    QuartzDashboardOptions options,
    IHttpClientFactory? httpClientFactory,
    ILogger<DashboardListenerAttacher> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var scheduler = await schedulerFactory.GetScheduler(ct);
            scheduler.ListenerManager.AddJobListener(new DashboardJobListener(
                eventBus,
                fireHistoryStore,
                logBuffer,
                bucketService,
                options,
                httpClientFactory));
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

internal sealed class DashboardJobListener(
    DashboardEventBus eventBus,
    IFireHistoryStore fireHistoryStore,
    ExecutionLogBuffer? logBuffer,
    ExecutionBucketService? bucketService,
    QuartzDashboardOptions options,
    IHttpClientFactory? httpClientFactory) : IJobListener
{
    public string Name => "QuartzDashboardListener";

    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken ct)
    {
        var jobKey = $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}";
        var triggerKey = $"{context.Trigger.Key.Group}.{context.Trigger.Key.Name}";
        logBuffer?.Append(jobKey, $"▶ Executing (trigger: {triggerKey})");

        // Publish trigger event with all fields the executing-jobs card needs
        eventBus.Publish(new JobTriggeredEvent(
            jobKey, triggerKey,
            context.JobDetail.Key.Name,
            context.JobDetail.Key.Group,
            context.Trigger.Key.Name,
            context.Trigger.Key.Group,
            context.JobDetail.JobType.Name,
            context.FireInstanceId,
            context.FireTimeUtc,
            context.ScheduledFireTimeUtc));
        return Task.CompletedTask;
    }

    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken ct) => Task.CompletedTask;

    public Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken ct)
    {
        var jobKey = $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}";
        var triggerKey = $"{context.Trigger.Key.Group}.{context.Trigger.Key.Name}";
        var duration = DateTimeOffset.UtcNow - context.FireTimeUtc;
        var success = jobException == null;

        // Record to fire history store
        fireHistoryStore.RecordFire(jobKey, triggerKey, context.FireTimeUtc, duration, success, context.RefireCount);

        // Update in-memory execution stats (buckets)
        bucketService?.Record(duration, success);

        // Log execution
        logBuffer?.Append(jobKey, success
            ? $"✓ Completed in {duration.TotalMilliseconds:F0}ms"
            : $"✗ Failed: {jobException?.Message?.Truncate(200) ?? "Unknown error"}");

        // Publish to event bus for SignalR
        eventBus.Publish(new JobExecutedEvent(jobKey, triggerKey, context.FireInstanceId, duration, success, context.FireTimeUtc));

        if (!success && jobException != null)
        {
            if (options.OnJobFailed != null)
            {
                _ = Task.Run(async () =>
                {
                    try { await options.OnJobFailed(jobKey, jobException); }
                    catch { /* never propagate callback exceptions */ }
                });
            }

            if (!string.IsNullOrWhiteSpace(options.WebhookUrl) && httpClientFactory != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var payload = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            jobKey,
                            triggerKey,
                            error = jobException.Message,
                            fireTime = context.FireTimeUtc,
                            durationMs = Math.Round(duration.TotalMilliseconds),
                            refireCount = context.RefireCount,
                        });

                        using var client = httpClientFactory.CreateClient();
                        using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                        await client.PostAsync(options.WebhookUrl, content, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                        // never propagate webhook exceptions
                    }
                });
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Extension helper to truncate strings safely
/// </summary>
internal static class StringExtensions
{
    public static string Truncate(this string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? "" : (value.Length <= maxLength ? value : value[..maxLength] + "...");
}
