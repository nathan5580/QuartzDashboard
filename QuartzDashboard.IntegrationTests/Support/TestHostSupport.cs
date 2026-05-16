using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace QuartzDashboard.IntegrationTests.Support;

internal sealed class DashboardTestScenario
{
    public string Path { get; init; } = "/quartz";
    public bool Enabled { get; init; } = true;
    public bool ReadOnly { get; init; }
    public bool UseSignalR { get; init; } = true;
    public bool RequireAuthentication { get; init; }
    public bool RequireCsrfHeader { get; init; }
    public string[] AllowedRoles { get; init; } = [];
    public string RequiredPolicy { get; init; } = string.Empty;
    public bool EnableOnAuthorize { get; init; }
    public bool AllowOnAuthorize { get; init; } = true;
    public string Title { get; init; } = "QuartzDash Integration";
    public int MaxFireHistory { get; init; } = 500;
    public string SchedulerName { get; init; } = $"QuartzDashboardIntegration-{Guid.NewGuid():N}";
    public string? WebhookUrl { get; init; }

    public static DashboardTestScenario FromConfiguration(IConfiguration configuration)
    {
        var allowedRoles = configuration["QuartzDashboardIntegration:AllowedRoles"];

        return new DashboardTestScenario
        {
            Path = configuration["QuartzDashboardIntegration:Path"] ?? "/quartz",
            Enabled = configuration.GetValue("QuartzDashboardIntegration:Enabled", true),
            ReadOnly = configuration.GetValue("QuartzDashboardIntegration:ReadOnly", false),
            UseSignalR = configuration.GetValue("QuartzDashboardIntegration:UseSignalR", true),
            RequireAuthentication = configuration.GetValue("QuartzDashboardIntegration:RequireAuthentication", false),
            RequireCsrfHeader = configuration.GetValue("QuartzDashboardIntegration:RequireCsrfHeader", false),
            AllowedRoles = string.IsNullOrWhiteSpace(allowedRoles)
                ? []
                : allowedRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            RequiredPolicy = configuration["QuartzDashboardIntegration:RequiredPolicy"] ?? string.Empty,
            EnableOnAuthorize = configuration.GetValue("QuartzDashboardIntegration:EnableOnAuthorize", false),
            AllowOnAuthorize = configuration.GetValue("QuartzDashboardIntegration:AllowOnAuthorize", true),
            Title = configuration["QuartzDashboardIntegration:Title"] ?? "QuartzDash Integration",
            MaxFireHistory = configuration.GetValue("QuartzDashboardIntegration:MaxFireHistory", 500),
            SchedulerName = configuration["QuartzDashboardIntegration:SchedulerName"] ?? $"QuartzDashboardIntegration-{Guid.NewGuid():N}",
            WebhookUrl = configuration["QuartzDashboardIntegration:WebhookUrl"]
        };
    }

    public void Apply(QuartzDashboardOptions options, IServiceCollection services)
    {
        options.Path = Path;
        options.Enabled = Enabled;
        options.ReadOnly = ReadOnly;
        options.UseSignalR = UseSignalR;
        options.RequireAuthentication = RequireAuthentication;
        options.RequireCsrfHeader = RequireCsrfHeader;
        options.AllowedRoles = AllowedRoles;
        options.RequiredPolicy = RequiredPolicy;
        options.Title = Title;
        options.MaxFireHistory = MaxFireHistory;
        options.WebhookUrl = WebhookUrl;
        if (EnableOnAuthorize)
        {
            options.OnAuthorize = context =>
            {
                context.RequestServices.GetRequiredService<OnAuthorizeTracker>().Increment();
                return AllowOnAuthorize;
            };
        }
    }
}

internal sealed class OnAuthorizeTracker
{
    private int _count;

    public int Count => _count;

    public void Increment() => Interlocked.Increment(ref _count);
}

internal sealed class JobExecutionTracker
{
    private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

    public int Increment(string jobKey) => _counts.AddOrUpdate(jobKey, 1, (_, current) => current + 1);
}

internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var userValues) || string.IsNullOrWhiteSpace(userValues[0]))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userValues[0]!),
            new(ClaimTypes.Name, userValues[0]!)
        };

        if (Request.Headers.TryGetValue("X-Test-Roles", out var roleValues) && !string.IsNullOrWhiteSpace(roleValues[0]))
        {
            claims.AddRange(roleValues[0]!
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim(ClaimTypes.Role, role)));
        }

        if (Request.Headers.TryGetValue("X-Test-Permissions", out var permissionValues) && !string.IsNullOrWhiteSpace(permissionValues[0]))
        {
            claims.AddRange(permissionValues[0]!
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(permission => new Claim("permission", permission)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

[DisallowConcurrentExecution]
internal sealed class FastJob(ILogger<FastJob> logger, JobExecutionTracker tracker) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        tracker.Increment("demo.FastJob");
        logger.LogInformation("FastJob executed");
        await Task.Delay(50, context.CancellationToken);
    }
}

[DisallowConcurrentExecution]
internal sealed class SlowJob(ILogger<SlowJob> logger, JobExecutionTracker tracker) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        tracker.Increment("demo.SlowJob");
        logger.LogInformation("SlowJob executed");
        await Task.Delay(150, context.CancellationToken);
    }
}

[DisallowConcurrentExecution]
internal sealed class FlakyJob(ILogger<FlakyJob> logger, JobExecutionTracker tracker) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var run = tracker.Increment("demo.FlakyJob");
        logger.LogInformation("FlakyJob executed {Run}", run);
        await Task.Delay(80, context.CancellationToken);
        if (run % 2 == 1)
            throw new InvalidOperationException("Deterministic flaky failure");
    }
}

[DisallowConcurrentExecution]
internal sealed class CronJob(ILogger<CronJob> logger, JobExecutionTracker tracker) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        tracker.Increment("demo.CronJob");
        logger.LogInformation("CronJob executed");
        await Task.Delay(40, context.CancellationToken);
    }
}

internal sealed class ManualJob(ILogger<ManualJob> logger, JobExecutionTracker tracker) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        tracker.Increment("demo.ManualJob");
        logger.LogInformation("ManualJob executed");
        await Task.Delay(25, context.CancellationToken);
    }
}
