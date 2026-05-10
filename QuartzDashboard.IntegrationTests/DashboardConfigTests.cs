using System.Net;
using QuartzDashboard.IntegrationTests.Fixtures;
using Xunit;

namespace QuartzDashboard.IntegrationTests;

[Collection(QuartzDashboardIntegrationCollection.Name)]
public sealed class DashboardConfigTests
{
    [Fact]
    public async Task GetDashboard_CustomPath_WorksAtConfiguredLocation()
    {
        await using var customFactory = new TestWebAppFactory(options => options.Path = "/admin/scheduler");
        using var client = customFactory.CreateAnonymousClient(allowAutoRedirect: false);

        using var configuredResponse = await client.GetAsync("/admin/scheduler/");
        using var oldPathResponse = await client.GetAsync("/quartz/");

        Assert.Equal(HttpStatusCode.OK, configuredResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, oldPathResponse.StatusCode);
    }

    [Fact]
    public async Task GetConfig_ReadOnlyMode_ReturnsReadOnlyTrue()
    {
        await using var customFactory = new TestWebAppFactory(options => options.ReadOnly = true);
        using var client = customFactory.CreateAnonymousClient();

        using var response = await client.GetAsync("/quartz/api/config");
        using var json = await response.ReadJsonAsync();

        Assert.True(json.RootElement.GetProperty("readOnly").GetBoolean());
    }

    [Fact]
    public async Task GetDashboard_EnabledFalse_DoesNotRegisterRoutes()
    {
        await using var customFactory = new TestWebAppFactory(options => options.Enabled = false);
        using var client = customFactory.CreateAnonymousClient();

        using var dashboardResponse = await client.GetAsync("/quartz/");
        using var hostResponse = await client.GetAsync("/api/weather");

        Assert.Equal(HttpStatusCode.NotFound, dashboardResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, hostResponse.StatusCode);
    }

    [Fact]
    public async Task GetHub_UseSignalRFalse_HubIsNotMapped()
    {
        await using var customFactory = new TestWebAppFactory(options => options.UseSignalR = false);
        using var client = customFactory.CreateAnonymousClient();

        using var response = await client.PostAsync("/quartz/hub/negotiate?negotiateVersion=1", new StringContent(string.Empty));
        using var configResponse = await client.GetAsync("/quartz/api/config");
        using var config = await configResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(config.RootElement.GetProperty("useSignalR").GetBoolean());
    }

    [Fact]
    public async Task GetDashboard_CustomTitle_IsReflectedInConfigAndHtml()
    {
        await using var customFactory = new TestWebAppFactory(options => options.Title = "Operations Scheduler");
        using var client = customFactory.CreateAnonymousClient();

        using var configResponse = await client.GetAsync("/quartz/api/config");
        using var config = await configResponse.ReadJsonAsync();
        var html = await client.GetStringAsync("/quartz/");

        Assert.Equal("Operations Scheduler", config.RootElement.GetProperty("title").GetString());
        Assert.Contains("Operations Scheduler", html);
    }

    [Fact]
    public async Task GetConfig_WebhookUrlConfigured_DoesNotExposeSecret()
    {
        await using var customFactory = new TestWebAppFactory(options =>
            options.WebhookUrl = "https://hooks.example.test/services/sensitive-token");
        using var client = customFactory.CreateAnonymousClient();

        using var response = await client.GetAsync("/quartz/api/config");
        using var config = await response.ReadJsonAsync();

        Assert.True(config.RootElement.GetProperty("hasWebhookConfigured").GetBoolean());
        Assert.False(config.RootElement.TryGetProperty("webhookUrl", out _));
    }

    [Fact]
    public async Task GetHistory_MaxFireHistory_IsRespected()
    {
        await using var customFactory = new TestWebAppFactory(options => options.MaxFireHistory = 3);
        await customFactory.StartServerAsync();

        using var client = customFactory.CreateAnonymousClient();

        for (var i = 0; i < 6; i++)
            await client.TriggerManualJobAsync();

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < expiresAt)
        {
            using var response = await client.GetAsync("/quartz/api/history?limit=50");
            using var json = await response.ReadJsonAsync();
            var total = json.RootElement.GetProperty("total").GetInt32();
            if (total == 3)
                return;

            await Task.Delay(250);
        }

        throw new TimeoutException("Fire history was not capped at MaxFireHistory.");
    }
}
