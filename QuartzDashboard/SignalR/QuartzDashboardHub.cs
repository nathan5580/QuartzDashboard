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

/// <summary>
/// Bridges DashboardEventBus events to SignalR clients.
/// </summary>
internal sealed class DashboardSignalRBridge(
    IHubContext<QuartzDashboardHub> hubContext,
    DashboardEventBus eventBus,
    ILogger<DashboardSignalRBridge> logger) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        eventBus.OnJobExecuted += async e =>
        {
            try
            {
                await hubContext.Clients.Group(QuartzDashboardHub.GroupName)
                    .SendAsync("jobExecuted", new
                    {
                        jobKey = e.JobKey,
                        triggerKey = e.TriggerKey,
                        duration = e.Duration.TotalMilliseconds,
                        success = e.Success,
                        fireTime = e.FireTime,
                    }, ct);
            }
            catch (Exception ex) { logger.LogWarning(ex, "SignalR send failed (jobExecuted)"); }
        };

        eventBus.OnJobTriggered += async e =>
        {
            try
            {
                await hubContext.Clients.Group(QuartzDashboardHub.GroupName)
                    .SendAsync("jobTriggered", new
                    {
                        jobKey = e.JobKey,
                        triggerKey = e.TriggerKey,
                        fireTime = e.FireTime,
                    }, ct);
            }
            catch (Exception ex) { logger.LogWarning(ex, "SignalR send failed (jobTriggered)"); }
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

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
