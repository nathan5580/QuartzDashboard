using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using QuartzDashboard.Internal;
using QuartzDashboard.Abstractions;
using QuartzDashboard.Services;

namespace QuartzDashboard;

/// <summary>
/// Extension methods for registering the Quartz Dashboard services.
/// Call <c>builder.Services.AddQuartzDashboard()</c> after <c>AddQuartz()</c>.
/// </summary>
public static class QuartzDashboardServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services required by Quartz Dashboard, including history storage,
    /// execution logging, event publishing, and optional SignalR updates.
    /// </summary>
    /// <param name="services">The service collection to add Quartz Dashboard services to.</param>
    /// <param name="configure">An optional callback used to configure <see cref="QuartzDashboardOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so calls can be chained.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddQuartz();
    /// builder.Services.AddQuartzHostedService();
    /// builder.Services.AddQuartzDashboard(options =&gt;
    /// {
    ///     options.Path = "/quartz";
    ///     options.RequireAuthentication = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddQuartzDashboard(this IServiceCollection services, Action<QuartzDashboardOptions>? configure = null)
    {
        var options = new QuartzDashboardOptions();
        configure?.Invoke(options);
        ValidateOptions(options);
        services.AddSingleton(options);
        // Also expose the read-only contract so handlers and custom integrations can opt out
        // of accidentally mutating the configured options at runtime.
        services.AddSingleton<IQuartzDashboardOptions>(options);
        services.AddHttpClient();

        // Fire history store — JSON file if PersistHistoryPath is set, otherwise in-memory.
        // For SQLite persistence, add the Dot.QuartzDashboard.Sqlite package and call
        // services.AddQuartzDashboardSqliteHistory(...) AFTER AddQuartzDashboard().
        if (!string.IsNullOrWhiteSpace(options.PersistHistoryPath))
            services.AddSingleton<IFireHistoryStore>(sp => new FileFireHistoryStore(
                options.PersistHistoryPath,
                sp.GetRequiredService<ILogger<FileFireHistoryStore>>(),
                options.MaxFireHistory,
                options.HistoryRetentionHours));
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
    /// No-op retained for backwards compatibility because history registration is now handled by
    /// <see cref="AddQuartzDashboard(IServiceCollection, Action{QuartzDashboardOptions}?)"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance.</returns>
    [Obsolete("History is registered automatically by AddQuartzDashboard(). This call is no longer needed.")]
    public static IServiceCollection AddQuartzDashboardHistory(this IServiceCollection services) => services;

    private static void ValidateOptions(QuartzDashboardOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Path))
            throw new ArgumentException("QuartzDashboardOptions.Path must be non-empty.", nameof(options));

        if (!options.Path.StartsWith('/'))
            throw new ArgumentException($"QuartzDashboardOptions.Path must start with '/' (got '{options.Path}').", nameof(options));

        if (options.MaxFireHistory < 0)
            throw new ArgumentException($"QuartzDashboardOptions.MaxFireHistory must be >= 0 (got {options.MaxFireHistory}).", nameof(options));

        if (options.MaxExecutionLogsPerJob < 0)
            throw new ArgumentException($"QuartzDashboardOptions.MaxExecutionLogsPerJob must be >= 0 (got {options.MaxExecutionLogsPerJob}).", nameof(options));

        if (options.HistoryRetentionHours < 0)
            throw new ArgumentException($"QuartzDashboardOptions.HistoryRetentionHours must be >= 0 (got {options.HistoryRetentionHours}).", nameof(options));

        if (!string.IsNullOrWhiteSpace(options.WebhookUrl))
        {
            if (!Uri.TryCreate(options.WebhookUrl, UriKind.Absolute, out var webhookUri))
                throw new ArgumentException($"QuartzDashboardOptions.WebhookUrl must be an absolute URI (got '{options.WebhookUrl}').", nameof(options));
            if (webhookUri.Scheme != Uri.UriSchemeHttp && webhookUri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException($"QuartzDashboardOptions.WebhookUrl must use http or https (got scheme '{webhookUri.Scheme}').", nameof(options));
        }
    }
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
    ILogger<DashboardJobListener> jobListenerLogger,
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
                httpClientFactory,
                jobListenerLogger));
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
    IHttpClientFactory? httpClientFactory,
    ILogger<DashboardJobListener> logger) : IJobListener
{
    private readonly ILogger<DashboardJobListener> _logger = logger;

    private static readonly System.Text.Json.JsonSerializerOptions WebhookJsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    };

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
        fireHistoryStore.RecordFire(jobKey, triggerKey, context.FireTimeUtc, duration, success, context.RefireCount,
            jobException?.InnerException?.Message ?? jobException?.Message,
            jobException?.InnerException?.GetType().Name ?? jobException?.GetType().Name);

        // Update in-memory execution stats (buckets)
        bucketService?.Record(duration, success);

        // Log execution. The single-line summary is kept short so the History UI snippet
        // reads cleanly; the inner-message line and the stack trace are stored in full so
        // the detail modal has the diagnostic frames it needs. Each entry is one ring-buffer
        // slot bounded by MaxExecutionLogsPerJob, so total memory stays bounded.
        logBuffer?.Append(jobKey, success
            ? $"✓ Completed in {duration.TotalMilliseconds:F0}ms"
            : $"✗ Failed: {jobException?.Message?.Truncate(200) ?? "Unknown error"}");

        if (!success && jobException != null)
        {
            var inner = jobException.InnerException;
            if (inner != null)
                logBuffer?.Append(jobKey, $"  └─ {inner.GetType().Name}: {inner.Message}");
            var stackTrace = inner?.StackTrace ?? jobException.StackTrace;
            if (!string.IsNullOrEmpty(stackTrace))
                logBuffer?.Append(jobKey, stackTrace);
        }

        // Publish to event bus for SignalR
        eventBus.Publish(new JobExecutedEvent(jobKey, triggerKey, context.FireInstanceId, duration, success, context.FireTimeUtc,
            jobException?.InnerException?.Message ?? jobException?.Message));

        if (!success && jobException != null)
        {
            if (options.OnJobFailed != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await options.OnJobFailed(jobKey, jobException);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "OnJobFailed callback threw an exception for job {JobKey}", jobKey);
                    }
                });
            }

            if (!string.IsNullOrWhiteSpace(options.WebhookUrl) && httpClientFactory != null)
            {
                var webhookUrl = options.WebhookUrl;
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
                        }, WebhookJsonOptions);

                        using var client = httpClientFactory.CreateClient();
                        using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                        await client.PostAsync(webhookUrl, content, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to POST webhook to {Url}", webhookUrl);
                    }
                });
            }
        }

        return Task.CompletedTask;
    }
}
