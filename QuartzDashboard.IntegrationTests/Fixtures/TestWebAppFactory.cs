using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz.Logging;
using QuartzDashboard.IntegrationTests.Support;
using Xunit;

namespace QuartzDashboard.IntegrationTests.Fixtures;

public sealed class TestWebAppFactory(Action<TestScenarioBuilder>? configure = null) : WebApplicationFactory<Program>
{
    private readonly Action<TestScenarioBuilder>? _configure = configure;

    static TestWebAppFactory()
    {
        LogProvider.SetCurrentLogProvider(SilentQuartzLogProvider.Instance);
    }

    protected override IHostBuilder CreateHostBuilder()
        => Program.CreateHostBuilder();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var scenario = new TestScenarioBuilder();
        _configure?.Invoke(scenario);
        scenario.SchedulerName ??= $"QuartzDashboardIntegration-{Guid.NewGuid():N}";

        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(scenario.ToDictionary());
        });
    }
}

file sealed class SilentQuartzLogProvider : ILogProvider
{
    public static SilentQuartzLogProvider Instance { get; } = new();

    public Logger GetLogger(string name) => (_, _, _, _) => false;

    public IDisposable OpenNestedContext(string message) => NullScope.Instance;

    public IDisposable OpenMappedContext(string key, object value, bool destructure = false) => NullScope.Instance;

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

public sealed class TestScenarioBuilder
{
    public string Path { get; set; } = "/quartz";
    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool UseSignalR { get; set; } = true;
    public bool RequireAuthentication { get; set; }
    public string[] AllowedRoles { get; set; } = [];
    public string RequiredPolicy { get; set; } = string.Empty;
    public bool EnableOnAuthorize { get; set; }
    public bool AllowOnAuthorize { get; set; } = true;
    public string Title { get; set; } = "QuartzDash Integration";
    public int MaxFireHistory { get; set; } = 500;
    public string? SchedulerName { get; set; }

    internal Dictionary<string, string?> ToDictionary() => new()
    {
        ["QuartzDashboardIntegration:Path"] = Path,
        ["QuartzDashboardIntegration:Enabled"] = Enabled.ToString(),
        ["QuartzDashboardIntegration:ReadOnly"] = ReadOnly.ToString(),
        ["QuartzDashboardIntegration:UseSignalR"] = UseSignalR.ToString(),
        ["QuartzDashboardIntegration:RequireAuthentication"] = RequireAuthentication.ToString(),
        ["QuartzDashboardIntegration:AllowedRoles"] = string.Join(',', AllowedRoles),
        ["QuartzDashboardIntegration:RequiredPolicy"] = RequiredPolicy,
        ["QuartzDashboardIntegration:EnableOnAuthorize"] = EnableOnAuthorize.ToString(),
        ["QuartzDashboardIntegration:AllowOnAuthorize"] = AllowOnAuthorize.ToString(),
        ["QuartzDashboardIntegration:Title"] = Title,
        ["QuartzDashboardIntegration:MaxFireHistory"] = MaxFireHistory.ToString(),
        ["QuartzDashboardIntegration:SchedulerName"] = SchedulerName
    };
}

public static class TestWebAppFactoryExtensions
{
    private static void ResetQuartzLoggerProvider()
        => LogProvider.SetCurrentLogProvider(SilentQuartzLogProvider.Instance);

    public static HttpClient CreateAnonymousClient(this TestWebAppFactory factory, bool allowAutoRedirect = true)
    {
        ResetQuartzLoggerProvider();
        return factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowAutoRedirect });
    }

    public static HttpClient CreateAuthenticatedClient(
        this TestWebAppFactory factory,
        string user = "admin@example.com",
        IEnumerable<string>? roles = null,
        IEnumerable<string>? permissions = null,
        bool allowAutoRedirect = true)
    {
        var client = factory.CreateAnonymousClient(allowAutoRedirect);
        client.DefaultRequestHeaders.Add("X-Test-User", user);

        var rolesValue = string.Join(',', roles ?? []);
        if (!string.IsNullOrWhiteSpace(rolesValue))
            client.DefaultRequestHeaders.Add("X-Test-Roles", rolesValue);

        var permissionsValue = string.Join(',', permissions ?? []);
        if (!string.IsNullOrWhiteSpace(permissionsValue))
            client.DefaultRequestHeaders.Add("X-Test-Permissions", permissionsValue);

        return client;
    }

    public static async Task StartServerAsync(this TestWebAppFactory factory, TimeSpan? startupDelay = null)
    {
        ResetQuartzLoggerProvider();
        _ = factory.Server;
        await Task.Delay(startupDelay ?? TimeSpan.FromMilliseconds(500));
    }

    public static async Task<JsonDocument> ReadJsonAsync(this HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(payload);
    }

    public static HubConnection CreateHubConnection(
        this TestWebAppFactory factory,
        string basePath = "/quartz",
        string? user = null,
        IEnumerable<string>? roles = null,
        IEnumerable<string>? permissions = null)
    {
        ResetQuartzLoggerProvider();
        _ = factory.Server;

        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress!, $"{basePath.TrimEnd('/')}/hub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                if (!string.IsNullOrWhiteSpace(user))
                    options.Headers["X-Test-User"] = user;

                var rolesValue = string.Join(',', roles ?? []);
                if (!string.IsNullOrWhiteSpace(rolesValue))
                    options.Headers["X-Test-Roles"] = rolesValue;

                var permissionsValue = string.Join(',', permissions ?? []);
                if (!string.IsNullOrWhiteSpace(permissionsValue))
                    options.Headers["X-Test-Permissions"] = permissionsValue;
            })
            .Build();
    }

    public static int GetAuthorizeCount(this TestWebAppFactory factory)
        => factory.Services.GetRequiredService<OnAuthorizeTracker>().Count;

    public static async Task WaitForHistoryAsync(
        this TestWebAppFactory factory,
        int minimumCount,
        string basePath = "/quartz",
        TimeSpan? timeout = null)
    {
        await factory.StartServerAsync(TimeSpan.Zero);

        using var client = factory.CreateAnonymousClient();
        var expiresAt = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));

        while (DateTimeOffset.UtcNow < expiresAt)
        {
            using var response = await client.GetAsync($"{basePath.TrimEnd('/')}/api/history?limit=200");
            if (response.IsSuccessStatusCode)
            {
                using var json = await response.ReadJsonAsync();
                if (json.RootElement.GetProperty("data").GetArrayLength() >= minimumCount)
                    return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for {minimumCount} history entries at {basePath}.");
    }

    public static async Task TriggerManualJobAsync(this HttpClient client, string basePath = "/quartz")
    {
        using var response = await client.PostAsync($"{basePath.TrimEnd('/')}/api/jobs/demo/ManualJob/trigger", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
    }

    public static void AssertJsonContentType(this HttpResponseMessage response)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.NotNull(contentType);
        Assert.Equal("application/json", contentType);
    }

    public static void AssertHtmlContentType(this HttpResponseMessage response)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.NotNull(contentType);
        Assert.Equal("text/html", contentType);
    }

    public static void AssertTextContentType(this HttpResponseMessage response, string expectedMediaType)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.NotNull(contentType);
        Assert.Equal(expectedMediaType, contentType);
    }
}
