using Microsoft.Extensions.Logging;
using Quartz;
using QuartzDashboard.Internal;

namespace QuartzDashboard;

/// <summary>
/// Listens to Quartz scheduler lifecycle events and publishes them to the DashboardEventBus.
/// Also updates the in-memory fire history and execution buckets.
/// </summary>
internal sealed class DashboardSchedulerListener(
    DashboardEventBus eventBus) : ISchedulerListener
{
    public Task JobAdded(IJobDetail jobDetail, CancellationToken ct)
    {
        eventBus.Publish(new JobsUpdatedEvent());
        return Task.CompletedTask;
    }

    public Task JobDeleted(JobKey jobKey, CancellationToken ct)
    {
        eventBus.Publish(new JobsUpdatedEvent());
        return Task.CompletedTask;
    }

    public Task JobPaused(JobKey jobKey, CancellationToken ct)
    {
        eventBus.Publish(new JobsUpdatedEvent());
        return Task.CompletedTask;
    }

    public Task JobResumed(JobKey jobKey, CancellationToken ct)
    {
        eventBus.Publish(new JobsUpdatedEvent());
        return Task.CompletedTask;
    }

    public Task JobScheduled(ITrigger trigger, CancellationToken ct)
    {
        eventBus.Publish(new JobsUpdatedEvent());
        return Task.CompletedTask;
    }

    public Task JobsPaused(string jobGroup, CancellationToken ct)
    {
        eventBus.Publish(new JobsUpdatedEvent());
        return Task.CompletedTask;
    }

    public Task JobsResumed(string jobGroup, CancellationToken ct)
    {
        eventBus.Publish(new JobsUpdatedEvent());
        return Task.CompletedTask;
    }

    public Task JobUnscheduled(TriggerKey triggerKey, CancellationToken ct)
    {
        eventBus.Publish(new JobsUpdatedEvent());
        return Task.CompletedTask;
    }

    public Task SchedulerInStandbyMode(CancellationToken ct)
    {
        eventBus.Publish(new SchedulerStatusEvent(true, true, false));
        return Task.CompletedTask;
    }

    public Task SchedulerShutdown(CancellationToken ct)
    {
        eventBus.Publish(new SchedulerStatusEvent(false, false, true));
        return Task.CompletedTask;
    }

    public Task SchedulerStarted(CancellationToken ct)
    {
        eventBus.Publish(new SchedulerStatusEvent(true, false, false));
        return Task.CompletedTask;
    }

    public Task SchedulerStarting(CancellationToken ct) => Task.CompletedTask;

    public Task SchedulerShuttingdown(CancellationToken ct) => Task.CompletedTask;

    public Task SchedulerError(string msg, SchedulerException cause, CancellationToken ct) => Task.CompletedTask;

    public Task SchedulingDataCleared(CancellationToken ct) => Task.CompletedTask;

    public Task JobInterrupted(JobKey jobKey, CancellationToken ct) => Task.CompletedTask;

    public Task TriggerFinalized(ITrigger trigger, CancellationToken ct) => Task.CompletedTask;

    public Task TriggerMisfired(ITrigger trigger, CancellationToken ct) => Task.CompletedTask;

    public Task TriggerPaused(TriggerKey triggerKey, CancellationToken ct)
    {
        eventBus.Publish(new JobsUpdatedEvent());
        return Task.CompletedTask;
    }

    public Task TriggerResumed(TriggerKey triggerKey, CancellationToken ct)
    {
        eventBus.Publish(new JobsUpdatedEvent());
        return Task.CompletedTask;
    }

    public Task TriggersPaused(string? triggerGroup, CancellationToken ct)
    {
        eventBus.Publish(new JobsUpdatedEvent());
        return Task.CompletedTask;
    }

    public Task TriggersResumed(string? triggerGroup, CancellationToken ct)
    {
        eventBus.Publish(new JobsUpdatedEvent());
        return Task.CompletedTask;
    }
}
