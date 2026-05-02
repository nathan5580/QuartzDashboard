using Quartz;
using QuartzDashboard;

var builder = WebApplication.CreateBuilder(args);

// Add Quartz with some demo jobs
builder.Services.AddQuartz(q =>
{
    // Demo job 1: runs every 30 seconds
    var jobKey1 = new JobKey("HealthCheckJob");
    q.AddJob<DemoJob>(opts => opts.WithIdentity(jobKey1));
    q.AddTrigger(opts => opts
        .ForJob(jobKey1)
        .WithIdentity("HealthCheckJob-trigger")
        .WithSimpleSchedule(x => x.WithIntervalInSeconds(30).RepeatForever()));

    // Demo job 2: runs every minute
    var jobKey2 = new JobKey("CleanupJob");
    q.AddJob<DemoJob2>(opts => opts.WithIdentity(jobKey2));
    q.AddTrigger(opts => opts
        .ForJob(jobKey2)
        .WithIdentity("CleanupJob-trigger")
        .WithSimpleSchedule(x => x.WithIntervalInMinutes(1).RepeatForever()));

    // Demo job 3: runs every 2 minutes (durable, no trigger — must be triggered manually)
    q.AddJob<DemoJob3>(opts => opts
        .WithIdentity(new JobKey("ManualJob"))
        .StoreDurably());
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Add the Quartz Dashboard
builder.Services.AddQuartzDashboard(options =>
{
    options.Path = "/quartz";
});
builder.Services.AddQuartzDashboardHistory();

var app = builder.Build();

app.UseRouting();
app.UseQuartzDashboard();

app.MapGet("/", () => "Quartz Dashboard Demo — go to <a href='/quartz'>/quartz</a>");

Console.WriteLine("Demo running at http://localhost:5190");
app.Run("http://localhost:5190");

// --- Demo jobs ---

[DisallowConcurrentExecution]
public class DemoJob(ILogger<DemoJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("HealthCheckJob executing at {Time}", DateTime.UtcNow);
        await Task.Delay(500); // Simulate work
    }
}

[DisallowConcurrentExecution]
public class DemoJob2(ILogger<DemoJob2> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("CleanupJob executing at {Time}", DateTime.UtcNow);
        await Task.Delay(2000); // Simulate longer work
    }
}

public class DemoJob3(ILogger<DemoJob3> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("ManualJob triggered at {Time}", DateTime.UtcNow);
        await Task.Delay(100);
    }
}
