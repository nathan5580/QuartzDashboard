using Quartz;
using QuartzDashboard;

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  QuartzDashboard Sample — minimal setup proving the NuGet works
//  Just 3 lines to add the dashboard to any existing Quartz app:
//    1. builder.Services.AddQuartzDashboard();
//    2. app.UseRouting();
//    3. app.UseQuartzDashboard();
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

var builder = WebApplication.CreateBuilder(args);

// ── Standard Quartz setup ──────────────────────────────────────
builder.Services.AddQuartz(q =>
{
    q.AddJob<SampleJob>(j => j.WithIdentity("SampleJob").WithDescription("Runs every 10 seconds"));
    q.AddTrigger(t => t
        .ForJob("SampleJob")
        .WithIdentity("SampleJob-trigger")
        .WithSimpleSchedule(s => s.WithIntervalInSeconds(10).RepeatForever()));

    q.AddJob<SlowJob>(j => j.WithIdentity("SlowJob").WithDescription("Long-running job (2-4s)"));
    q.AddTrigger(t => t
        .ForJob("SlowJob")
        .WithIdentity("SlowJob-trigger")
        .WithSimpleSchedule(s => s.WithIntervalInSeconds(30).RepeatForever()));

    q.AddJob<FlakyJob>(j => j.WithIdentity("FlakyJob").WithDescription("Fails ~25% of the time"));
    q.AddTrigger(t => t
        .ForJob("FlakyJob")
        .WithIdentity("FlakyJob-trigger")
        .WithSimpleSchedule(s => s.WithIntervalInSeconds(15).RepeatForever()));
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// ── QuartzDashboard — one line to register ─────────────────────
builder.Services.AddQuartzDashboard(options =>
{
    options.Title = "My App Dashboard";          // Custom title in sidebar + browser tab
    options.HistoryRetentionHours = 48;          // Keep 48 hours of history
    // options.PersistHistoryPath = "quartz-history.json";  // Persist across restarts
    options.OnJobFailed = async (jobKey, ex) =>
    {
        Console.WriteLine($"[ALERT] Job failed: {jobKey} — {ex.Message}");
        await Task.CompletedTask;
    };
});

var app = builder.Build();

// ── QuartzDashboard — two lines to activate ────────────────────
app.UseRouting();
app.UseQuartzDashboard();

app.MapGet("/", () => Results.Redirect("/quartz"));

Console.WriteLine("""
    ┌─────────────────────────────────────────────┐
    │  QuartzDashboard Sample                     │
    │  Dashboard: http://localhost:5200/quartz    │
    │  3 jobs: SampleJob, SlowJob, FlakyJob       │
    └─────────────────────────────────────────────┘
    """);

app.Run("http://localhost:5200");

// ── Sample Jobs ────────────────────────────────────────────────

public class SampleJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await Task.Delay(Random.Shared.Next(50, 200));
    }
}

public class SlowJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await Task.Delay(Random.Shared.Next(2000, 4000));
    }
}

public class FlakyJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await Task.Delay(Random.Shared.Next(100, 500));
        if (Random.Shared.Next(100) < 25)
            throw new JobExecutionException("Random failure for testing");
    }
}
