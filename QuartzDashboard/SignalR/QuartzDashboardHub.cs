using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuartzDashboard.Internal;

namespace QuartzDashboard;

/// <summary>
/// SignalR hub used by dashboard clients to receive real-time scheduler and job execution updates.
/// </summary>
// Authorization is applied at the hub endpoint via MapHubEndpoint, which mirrors
// the dashboard's RequireAuthentication / RequiredPolicy / AllowedRoles. Method-level
// [Authorize] would override that and reject anonymous calls even when the host disables auth.
public class QuartzDashboardHub : Hub
{
    /// <summary>
    /// The SignalR group name used for dashboard update broadcasts.
    /// </summary>
    public const string GroupName = "dashboard";

    /// <summary>
    /// Adds the current connection to the dashboard broadcast group.
    /// </summary>
    /// <returns>A task that completes when the subscription has been registered.</returns>
    public async Task Subscribe() =>
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName);

    /// <summary>
    /// Removes the current connection from the dashboard broadcast group.
    /// </summary>
    /// <returns>A task that completes when the subscription has been removed.</returns>
    public async Task Unsubscribe() =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName);
}

// ===== Strongly-typed batched event records =====

internal sealed record ExecutedEvent(
    string JobKey,
    string TriggerKey,
    string FireInstanceId,
    double Duration,
    bool Success,
    DateTimeOffset FireTime,
    string? ExceptionMessage = null
);

internal sealed record TriggeredEvent(
    string JobKey,
    string TriggerKey,
    string JobName,
    string JobGroup,
    string TriggerName,
    string TriggerGroup,
    string JobType,
    string FireInstanceId,
    DateTimeOffset FireTime,
    DateTimeOffset? ScheduledFireTime
);

/// <summary>
/// Bridges DashboardEventBus events to SignalR clients using a Channel-based
/// producer/consumer pattern that batches events every 100ms.
/// </summary>
internal sealed class DashboardSignalRBridge(
    IHubContext<QuartzDashboardHub> hubContext,
    DashboardEventBus eventBus,
    ILogger<DashboardSignalRBridge> logger) : IHostedService
{
    private readonly Channel<object> _channel = Channel.CreateBounded<object>(new BoundedChannelOptions(1024)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    // Stored delegate references so StopAsync can unsubscribe and avoid a leak across
    // graceful host recycles. Without these refs, each StartAsync would attach new
    // closures and StopAsync would have nothing to detach — handlers would pile up
    // on the singleton DashboardEventBus.
    private Action<JobExecutedEvent>? _onJobExecuted;
    private Action<JobTriggeredEvent>? _onJobTriggered;
    private Action<SchedulerStatusEvent>? _onSchedulerStatus;
    private Action<JobsUpdatedEvent>? _onJobsUpdated;
    private CancellationTokenSource? _consumerCts;

    public Task StartAsync(CancellationToken ct)
    {
        _consumerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _onJobExecuted = e =>
        {
            if (!_channel.Writer.TryWrite(new ExecutedEvent(
                JobKey: e.JobKey,
                TriggerKey: e.TriggerKey,
                FireInstanceId: e.FireInstanceId,
                Duration: e.Duration.TotalMilliseconds,
                Success: e.Success,
                FireTime: e.FireTime,
                ExceptionMessage: e.ExceptionMessage
            )))
            {
                logger.LogWarning("Channel full, dropping jobExecuted event");
            }
        };

        _onJobTriggered = e =>
        {
            if (!_channel.Writer.TryWrite(new TriggeredEvent(
                JobKey: e.JobKey,
                TriggerKey: e.TriggerKey,
                JobName: e.JobName,
                JobGroup: e.JobGroup,
                TriggerName: e.TriggerName,
                TriggerGroup: e.TriggerGroup,
                JobType: e.JobType,
                FireInstanceId: e.FireInstanceId,
                FireTime: e.FireTime,
                ScheduledFireTime: e.ScheduledFireTime
            )))
            {
                logger.LogWarning("Channel full, dropping jobTriggered event");
            }
        };

        _onSchedulerStatus = async e =>
        {
            try
            {
                await hubContext.Clients.Group(QuartzDashboardHub.GroupName)
                    .SendAsync("schedulerStatus", new
                    {
                        isStarted = e.IsStarted,
                        isStandbyMode = e.IsStandbyMode,
                        isShutdown = e.IsShutdown,
                    }, _consumerCts.Token);
            }
            catch (Exception ex) { logger.LogWarning(ex, "SignalR send failed (schedulerStatus)"); }
        };

        _onJobsUpdated = async _ =>
        {
            try
            {
                await hubContext.Clients.Group(QuartzDashboardHub.GroupName)
                    .SendAsync("jobsUpdated", new { }, _consumerCts.Token);
            }
            catch (Exception ex) { logger.LogWarning(ex, "SignalR send failed (jobsUpdated)"); }
        };

        eventBus.OnJobExecuted += _onJobExecuted;
        eventBus.OnJobTriggered += _onJobTriggered;
        eventBus.OnSchedulerStatusChanged += _onSchedulerStatus;
        eventBus.OnJobsUpdated += _onJobsUpdated;

        // Consumer: batch events every 100ms using pattern matching
        _ = ConsumeChannelAsync(_consumerCts.Token);

        return Task.CompletedTask;
    }

    private async Task ConsumeChannelAsync(CancellationToken ct)
    {
        var executed = new List<object>(64);
        var triggered = new List<object>(64);
        var reader = _channel.Reader;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!await reader.WaitToReadAsync(ct)) break;

                var deadline = DateTime.UtcNow.AddMilliseconds(100);
                executed.Clear();
                triggered.Clear();

                // Drain available items
                while (reader.TryRead(out var item))
                {
                    switch (item)
                    {
                        case ExecutedEvent ee:
                            executed.Add(new
                            {
                                jobKey = ee.JobKey,
                                triggerKey = ee.TriggerKey,
                                fireInstanceId = ee.FireInstanceId,
                                duration = ee.Duration,
                                durationMs = ee.Duration,
                                success = ee.Success,
                                fireTime = ee.FireTime,
                                exceptionMessage = ee.ExceptionMessage,
                            });
                            break;
                        case TriggeredEvent te:
                            triggered.Add(new
                            {
                                jobKey = te.JobKey,
                                triggerKey = te.TriggerKey,
                                jobName = te.JobName,
                                jobGroup = te.JobGroup,
                                triggerName = te.TriggerName,
                                triggerGroup = te.TriggerGroup,
                                jobType = te.JobType,
                                fireInstanceId = te.FireInstanceId,
                                fireTime = te.FireTime,
                                scheduledFireTime = te.ScheduledFireTime,
                            });
                            break;
                    }
                    if (executed.Count + triggered.Count >= 64) break;
                }

                // If deadline hasn't passed, sleep briefly and collect more
                if (DateTime.UtcNow < deadline && executed.Count + triggered.Count < 64)
                {
                    await Task.Delay(deadline - DateTime.UtcNow, ct);
                    while (reader.TryRead(out var item))
                    {
                        switch (item)
                        {
                            case ExecutedEvent ee:
                                executed.Add(new { jobKey = ee.JobKey, triggerKey = ee.TriggerKey, fireInstanceId = ee.FireInstanceId, duration = ee.Duration, durationMs = ee.Duration, success = ee.Success, fireTime = ee.FireTime, exceptionMessage = ee.ExceptionMessage });
                                break;
                            case TriggeredEvent te:
                                triggered.Add(new { jobKey = te.JobKey, triggerKey = te.TriggerKey, jobName = te.JobName, jobGroup = te.JobGroup, triggerName = te.TriggerName, triggerGroup = te.TriggerGroup, jobType = te.JobType, fireInstanceId = te.FireInstanceId, fireTime = te.FireTime, scheduledFireTime = te.ScheduledFireTime });
                                break;
                        }
                        if (executed.Count + triggered.Count >= 64) break;
                    }
                }

                // Send batches
                if (executed.Count > 0)
                {
                    try { await hubContext.Clients.Group(QuartzDashboardHub.GroupName).SendAsync("jobExecutedBatch", executed.ToArray(), ct); }
                    catch (Exception ex) { logger.LogWarning(ex, "SignalR batch send failed"); }
                }
                if (triggered.Count > 0)
                {
                    try { await hubContext.Clients.Group(QuartzDashboardHub.GroupName).SendAsync("jobTriggeredBatch", triggered.ToArray(), ct); }
                    catch (Exception ex) { logger.LogWarning(ex, "SignalR batch send failed"); }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "SignalR channel consumer error"); }
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        // Unsubscribe handlers so the singleton DashboardEventBus doesn't keep this
        // bridge alive after the host recycles. Without these -= calls, a subsequent
        // StartAsync would stack a second set of handlers on top of the first.
        if (_onJobExecuted != null) eventBus.OnJobExecuted -= _onJobExecuted;
        if (_onJobTriggered != null) eventBus.OnJobTriggered -= _onJobTriggered;
        if (_onSchedulerStatus != null) eventBus.OnSchedulerStatusChanged -= _onSchedulerStatus;
        if (_onJobsUpdated != null) eventBus.OnJobsUpdated -= _onJobsUpdated;

        _onJobExecuted = null;
        _onJobTriggered = null;
        _onSchedulerStatus = null;
        _onJobsUpdated = null;

        _consumerCts?.Cancel();
        _consumerCts?.Dispose();
        _consumerCts = null;

        _channel.Writer.TryComplete();
        return Task.CompletedTask;
    }
}
