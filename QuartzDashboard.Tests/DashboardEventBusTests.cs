using QuartzDashboard.Internal;
using Xunit;

namespace QuartzDashboard.Tests;

public sealed class DashboardEventBusTests
{
    [Fact]
    public void Publish_JobExecutedEvent_RaisesTypedHandler()
    {
        var bus = new DashboardEventBus();
        JobExecutedEvent? captured = null;

        bus.OnJobExecuted += e => captured = e;

        var evt = new JobExecutedEvent(
            "jobs.test", "t.test", "fire-123",
            TimeSpan.FromSeconds(5), true,
            DateTimeOffset.UtcNow, "No error");

        bus.Publish(evt);

        Assert.NotNull(captured);
        Assert.Equal("jobs.test", captured!.JobKey);
        Assert.Equal("t.test", captured.TriggerKey);
        Assert.Equal(TimeSpan.FromSeconds(5), captured.Duration);
        Assert.True(captured.Success);
    }

    [Fact]
    public void Publish_JobExecutedEvent_RaisesGenericHandler()
    {
        var bus = new DashboardEventBus();
        DashboardEvent? captured = null;

        bus.OnEvent += (_, e) => captured = e;

        var evt = new JobExecutedEvent(
            "jobs.x", "t.x", "f-1",
            TimeSpan.Zero, false,
            DateTimeOffset.UtcNow);

        bus.Publish(evt);

        Assert.NotNull(captured);
        Assert.IsType<JobExecutedEvent>(captured);
    }

    [Fact]
    public void Publish_JobTriggeredEvent_RaisesTypedHandler()
    {
        var bus = new DashboardEventBus();
        JobTriggeredEvent? captured = null;

        bus.OnJobTriggered += e => captured = e;

        var evt = new JobTriggeredEvent(
            "jobs.a", "t.a", "JobA", "DEFAULT",
            "TriggerA", "DEFAULT", "MyJobType",
            "f-1", DateTimeOffset.UtcNow, null);

        bus.Publish(evt);

        Assert.NotNull(captured);
        Assert.Equal("jobs.a", captured!.JobKey);
        Assert.Equal("JobA", captured.JobName);
        Assert.Equal("MyJobType", captured.JobType);
    }

    [Fact]
    public void Publish_SchedulerStatusEvent_RaisesTypedHandler()
    {
        var bus = new DashboardEventBus();
        SchedulerStatusEvent? captured = null;

        bus.OnSchedulerStatusChanged += e => captured = e;

        var evt = new SchedulerStatusEvent(true, false, false);
        bus.Publish(evt);

        Assert.NotNull(captured);
        Assert.True(captured!.IsStarted);
        Assert.False(captured.IsStandbyMode);
        Assert.False(captured.IsShutdown);
    }

    [Fact]
    public void Publish_JobsUpdatedEvent_RaisesTypedHandler()
    {
        var bus = new DashboardEventBus();
        JobsUpdatedEvent? captured = null;

        bus.OnJobsUpdated += e => captured = e;

        bus.Publish(new JobsUpdatedEvent());

        Assert.NotNull(captured);
    }

    [Fact]
    public void Publish_GenericHandlerSeesAllEventTypes()
    {
        var bus = new DashboardEventBus();
        var received = new List<Type>();

        bus.OnEvent += (_, e) => received.Add(e.GetType());

        bus.Publish(new JobExecutedEvent("j", "t", "f", TimeSpan.Zero, true, DateTimeOffset.UtcNow));
        bus.Publish(new JobTriggeredEvent("j", "t", "n", "g", "tn", "tg", "jt", "f", DateTimeOffset.UtcNow, null));
        bus.Publish(new SchedulerStatusEvent(true, false, false));
        bus.Publish(new JobsUpdatedEvent());

        Assert.Equal(4, received.Count);
        Assert.Contains(typeof(JobExecutedEvent), received);
        Assert.Contains(typeof(JobTriggeredEvent), received);
        Assert.Contains(typeof(SchedulerStatusEvent), received);
        Assert.Contains(typeof(JobsUpdatedEvent), received);
    }

    [Fact]
    public void Publish_TypedHandlerDoesNotReceiveWrongType()
    {
        var bus = new DashboardEventBus();
        JobExecutedEvent? captured = null;

        bus.OnJobExecuted += e => captured = e;

        bus.Publish(new JobsUpdatedEvent());

        Assert.Null(captured);
    }

    [Fact]
    public void Publish_TimestampIsSetAutomatically()
    {
        var bus = new DashboardEventBus();
        DashboardEvent? captured = null;

        bus.OnEvent += (_, e) => captured = e;

        var before = DateTimeOffset.UtcNow;
        bus.Publish(new JobsUpdatedEvent());
        var after = DateTimeOffset.UtcNow;

        Assert.NotNull(captured);
        Assert.InRange(captured!.Timestamp, before.AddSeconds(-1), after.AddSeconds(1));
    }
}
