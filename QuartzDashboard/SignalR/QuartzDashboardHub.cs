using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuartzDashboard.Internal;

namespace QuartzDashboard;

/// <summary>
/// SignalR hub for real-time dashboard updates.
/// Clients call Subscribe() on connect, Unsubscribe() on disconnect.
/// </summary>
public class QuartzDashboardHub : Hub
{
    public const string GroupName = "dashboard";

    public async Task Subscribe() =>
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName);

    public async Task Unsubscribe() =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName);
}

// ===== Strongly-typed batched event records =====

internal sealed record ExecutedEvent(
    string JobKey,
    string TriggerKey,
    double Duration,
    bool Success,
    DateTimeOffset FireTime
);

internal sealed record TriggeredEvent(
    string JobKey,
    string TriggerKey,
    DateTimeOffset FireTime
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

    public Task StartAsync(CancellationToken ct)
    {
        // Producer: write typed event records to the channel
        eventBus.OnJobExecuted += e =>
        {
            if (!_channel.Writer.TryWrite(new ExecutedEvent(
                JobKey: e.JobKey,
                TriggerKey: e.TriggerKey,
                Duration: e.Duration.TotalMilliseconds,
                Success: e.Success,
                FireTime: e.FireTime
            )))
            {
                logger.LogWarning("Channel full, dropping jobExecuted event");
            }
        };

        eventBus.OnJobTriggered += e =>
        {
            if (!_channel.Writer.TryWrite(new TriggeredEvent(
                JobKey: e.JobKey,
                TriggerKey: e.TriggerKey,
                FireTime: e.FireTime
            )))
            {
                logger.LogWarning("Channel full, dropping jobTriggered event");
            }
        };

        eventBus.OnSchedulerStatusChanged += async e =>
        {
            try
            {
                await hubContext.Clients.Group(QuartzDashboardHub.GroupName)
                    .SendAsync("schedulerStatus", new
                    {
                        isStarted = e.IsStarted,
                        isStandbyMode = e.IsStandbyMode,
                        isShutdown = e.IsShutdown,
                    }, ct);
            }
            catch (Exception ex) { logger.LogWarning(ex, "SignalR send failed (schedulerStatus)"); }
        };

        eventBus.OnJobsUpdated += async _ =>
        {
            try
            {
                await hubContext.Clients.Group(QuartzDashboardHub.GroupName)
                    .SendAsync("jobsUpdated", new { }, ct);
            }
            catch (Exception ex) { logger.LogWarning(ex, "SignalR send failed (jobsUpdated)"); }
        };

        // Consumer: batch events every 100ms using pattern matching
        _ = ConsumeChannelAsync(ct);

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
                                duration = ee.Duration,
                                success = ee.Success,
                                fireTime = ee.FireTime,
                            });
                            break;
                        case TriggeredEvent te:
                            triggered.Add(new
                            {
                                jobKey = te.JobKey,
                                triggerKey = te.TriggerKey,
                                fireTime = te.FireTime,
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
                                executed.Add(new { jobKey = ee.JobKey, triggerKey = ee.TriggerKey, duration = ee.Duration, success = ee.Success, fireTime = ee.FireTime });
                                break;
                            case TriggeredEvent te:
                                triggered.Add(new { jobKey = te.JobKey, triggerKey = te.TriggerKey, fireTime = te.FireTime });
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
        _channel.Writer.TryComplete();
        return Task.CompletedTask;
    }
}
