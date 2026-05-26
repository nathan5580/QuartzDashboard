using Quartz;
using QuartzDashboard;
using QuartzDashboard.Sqlite;

// ===== CLI Argument Parsing =====
var version = typeof(QuartzDashboardOptions).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
var port = 5190;
var authMode = false;
var readOnlyMode = false;
var sqliteMode = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-p" when i + 1 < args.Length:
            port = int.Parse(args[++i]);
            break;
        case "--auth":
            authMode = true;
            break;
        case "--readonly":
            readOnlyMode = true;
            break;
        case "--sqlite":
            sqliteMode = true;
            break;
        case "--help" or "-h":
            Console.WriteLine($"""
                QuartzDashboard Demo v{version}
                Usage: dotnet run [options]
                
                Options:
                  -p <port>       Port to listen on (default: 5190)
                  --auth          Enable authentication mode
                  --readonly      Enable read-only mode
                  --sqlite        Enable SQLite history persistence (demo-history.db)
                  --help, -h      Show this help
                """);
            return;
    }
}

var builder = WebApplication.CreateBuilder(args);

// Add Quartz with a diverse set of demo jobs
builder.Services.AddQuartz(q =>
{
    // Job 1: runs every 15 seconds — fast execution to generate graph data
    var jobKey1 = new JobKey("HealthCheck");
    q.AddJob<HealthCheckJob>(opts => opts.WithIdentity(jobKey1));
    q.AddTrigger(opts => opts
        .ForJob(jobKey1)
        .WithIdentity("HealthCheck-trigger")
        .WithDescription("System health pulse, runs every 15s")
        .WithSimpleSchedule(x => x.WithIntervalInSeconds(15).RepeatForever()));

    // Job 2: runs every 30 seconds — medium duration for the graph
    var jobKey2 = new JobKey("CacheWarmup");
    q.AddJob<CacheWarmupJob>(opts => opts.WithIdentity(jobKey2));
    q.AddTrigger(opts => opts
        .ForJob(jobKey2)
        .WithIdentity("CacheWarmup-trigger")
        .WithDescription("Warms Redis cache, runs every 30s")
        .WithSimpleSchedule(x => x.WithIntervalInSeconds(30).RepeatForever()));

    // Job 3: runs every 2 minutes — longer execution for the dashboard
    var jobKey3 = new JobKey("ReportGeneration");
    q.AddJob<ReportGenerationJob>(opts => opts.WithIdentity(jobKey3));
    q.AddTrigger(opts => opts
        .ForJob(jobKey3)
        .WithIdentity("ReportGeneration-trigger")
        .WithDescription("Generates nightly report, runs every 2min")
        .WithSimpleSchedule(x => x.WithIntervalInMinutes(2).RepeatForever()));

    // Job 4: CRON-based — fires at :00 and :30 of every minute
    var jobKey4 = new JobKey("DataSync");
    q.AddJob<DataSyncJob>(opts => opts.WithIdentity(jobKey4));
    q.AddTrigger(opts => opts
        .ForJob(jobKey4)
        .WithIdentity("DataSync-CRON-trigger")
        .WithDescription("Syncs external data on a CRON schedule")
        .WithCronSchedule("0/30 * * * * ?"));

    // Job 5: durable with no trigger — must be triggered manually via the dashboard
    q.AddJob<ManualNotificationJob>(opts => opts
        .WithIdentity(new JobKey("ManualNotification"))
        .WithDescription("On-demand push notification — trigger me from the dashboard!")
        .StoreDurably());

    // Job 6: randomly failing — populates Health page error data
    var jobKey6 = new JobKey("UnstableImport");
    q.AddJob<UnstableImportJob>(opts => opts.WithIdentity(jobKey6));
    q.AddTrigger(opts => opts
        .ForJob(jobKey6)
        .WithIdentity("UnstableImport-trigger")
        .WithDescription("Flaky import that fails ~30% of the time")
        .WithSimpleSchedule(x => x.WithIntervalInSeconds(20).RepeatForever()));
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Add the Quartz Dashboard with options based on CLI flags
builder.Services.AddQuartzDashboard(options =>
{
    options.Path = "/quartz";
    options.ReadOnly = readOnlyMode;
    // v4.2.0 flipped the default to true. The demo should follow the CLI flag explicitly,
    // otherwise `dotnet run` (no flags) returns 401 with no auth schemes registered.
    options.RequireAuthentication = authMode;

    if (authMode)
    {
        options.AllowedRoles = ["Admin"];
    }
});

// SQLite history persistence (via the Dot.QuartzDashboard.Sqlite package).
// Call AFTER AddQuartzDashboard so the SQLite store replaces the default in-memory one.
if (sqliteMode)
{
    builder.Services.AddQuartzDashboardSqliteHistory("demo-history.db");
}

var app = builder.Build();

app.UseRouting();
app.UseQuartzDashboard();

app.MapGet("/", () => $"Quartz Dashboard Demo v{version} — go to <a href='/quartz'>/quartz</a>");

Console.WriteLine("╔══════════════════════════════════════════════╗");
Console.WriteLine($"║  {($"QuartzDashboard Demo v{version}").PadRight(42)}║");
Console.WriteLine($"║  Open http://localhost:{port}/quartz          ║");
Console.WriteLine("║  Flags:                                     ║");
Console.WriteLine($"║   ├─ Auth: {(authMode ? "enabled" : "disabled").PadRight(29)}║");
Console.WriteLine($"║   ├─ Read-only: {(readOnlyMode ? "yes" : "no").PadRight(26)}║");
Console.WriteLine($"║   └─ SQLite: {(sqliteMode ? "demo-history.db" : "disabled").PadRight(29)}║");
Console.WriteLine("║                                              ║");
Console.WriteLine("║  Jobs:                                       ║");
Console.WriteLine("║   ├─ HealthCheck         (every 15s, 300ms)  ║");
Console.WriteLine("║   ├─ CacheWarmup         (every 30s, 1-3s)   ║");
Console.WriteLine("║   ├─ ReportGeneration    (every 2min, 5s)    ║");
Console.WriteLine("║   ├─ DataSync            (CRON 0/30)         ║");
Console.WriteLine("║   ├─ UnstableImport      (every 20s, ~30% fail) ║");
Console.WriteLine("║   └─ ManualNotification  (trigger via UI)    ║");
Console.WriteLine("╚══════════════════════════════════════════════╝");
app.Run($"http://localhost:{port}");

// ===== Demo Jobs =====

/// <summary>Fast health check — runs every 15s, generates frequent graph data</summary>
[DisallowConcurrentExecution]
public class HealthCheckJob(ILogger<HealthCheckJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("[HealthCheck] System OK");
        await Task.Delay(Random.Shared.Next(100, 500));
    }
}

/// <summary>Medium cache warmup — runs every 30s, variable duration for the graph</summary>
[DisallowConcurrentExecution]
public class CacheWarmupJob(ILogger<CacheWarmupJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("[CacheWarmup] Warming Redis cache...");
        await Task.Delay(Random.Shared.Next(1000, 3000));
        logger.LogInformation("[CacheWarmup] Cache warmed");
    }
}

/// <summary>Long report generation — runs every 2min, creates visible duration spikes on the graph</summary>
public class ReportGenerationJob(ILogger<ReportGenerationJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("[ReportGeneration] Generating report...");
        await Task.Delay(Random.Shared.Next(4000, 6000));
        logger.LogInformation("[ReportGeneration] Report generated");
    }
}

/// <summary>CRON-based data sync — fires at :00 and :30</summary>
[DisallowConcurrentExecution]
public class DataSyncJob(ILogger<DataSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("[DataSync] Syncing external data...");
        await Task.Delay(Random.Shared.Next(500, 1500));
        logger.LogInformation("[DataSync] Sync complete");
    }
}

/// <summary>Manual notification — no trigger, fire from the dashboard UI</summary>
public class ManualNotificationJob(ILogger<ManualNotificationJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("[ManualNotification] Sending push notification...");
        await Task.Delay(200);
        logger.LogInformation("[ManualNotification] Notification sent!");
    }
}

/// <summary>Unstable import — fails ~30% of the time to populate Health page error data</summary>
public class UnstableImportJob(ILogger<UnstableImportJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("[UnstableImport] Starting data import...");
        await Task.Delay(Random.Shared.Next(300, 800));

        if (Random.Shared.Next(100) < 30)
        {
            logger.LogError("[UnstableImport] Import failed — connection timeout");
            throw new JobExecutionException("Simulated connection timeout to external API");
        }

        logger.LogInformation("[UnstableImport] Import completed successfully");
    }
}
