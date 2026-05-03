namespace QuartzDashboard.Internal;

/// <summary>
/// Base event for the dashboard event bus.
/// </summary>
public abstract record DashboardEvent
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record JobExecutedEvent(
    string JobKey,
    string TriggerKey,
    string FireInstanceId,
    TimeSpan Duration,
    bool Success,
    DateTimeOffset FireTime
) : DashboardEvent;

public sealed record JobTriggeredEvent(
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
) : DashboardEvent;

public sealed record SchedulerStatusEvent(
    bool IsStarted,
    bool IsStandbyMode,
    bool IsShutdown
) : DashboardEvent;

public sealed record JobsUpdatedEvent() : DashboardEvent;

/// <summary>
/// In-memory event bus that decouples Quartz listeners from the SignalR bridge.
/// Singleton — shared across all components.
/// </summary>
public sealed class DashboardEventBus
{
    public event Action<JobExecutedEvent>? OnJobExecuted;
    public event Action<JobTriggeredEvent>? OnJobTriggered;
    public event Action<SchedulerStatusEvent>? OnSchedulerStatusChanged;
    public event Action<JobsUpdatedEvent>? OnJobsUpdated;

    public void Publish<T>(T @event) where T : DashboardEvent
    {
        switch (@event)
        {
            case JobExecutedEvent e:
                OnJobExecuted?.Invoke(e);
                break;
            case JobTriggeredEvent e:
                OnJobTriggered?.Invoke(e);
                break;
            case SchedulerStatusEvent e:
                OnSchedulerStatusChanged?.Invoke(e);
                break;
            case JobsUpdatedEvent e:
                OnJobsUpdated?.Invoke(e);
                break;
        }
    }
}
