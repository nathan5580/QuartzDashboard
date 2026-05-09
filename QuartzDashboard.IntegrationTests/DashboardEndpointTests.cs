using System.Net;
using QuartzDashboard.IntegrationTests.Fixtures;
using Xunit;

namespace QuartzDashboard.IntegrationTests;

[Collection(QuartzDashboardIntegrationCollection.Name)]
public sealed class DashboardEndpointTests : IAsyncLifetime
{
    private TestWebAppFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestWebAppFactory();
        await _factory.StartServerAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetDashboardRoot_WithoutTrailingSlash_RedirectsToTrailingSlash()
    {
        using var client = _factory.CreateAnonymousClient(allowAutoRedirect: false);
        using var response = await client.GetAsync("/quartz");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/quartz/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task GetDashboardRoot_WithTrailingSlash_ReturnsHtml()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        response.AssertHtmlContentType();
        Assert.Contains("alpine.min.js", html);
        Assert.Contains("app.js", html);
    }

    [Theory]
    [InlineData("/quartz/api/scheduler")]
    [InlineData("/quartz/api/jobs")]
    [InlineData("/quartz/api/triggers")]
    [InlineData("/quartz/api/history")]
    [InlineData("/quartz/api/stats")]
    [InlineData("/quartz/api/health")]
    [InlineData("/quartz/api/timeline")]
    [InlineData("/quartz/api/calendars")]
    [InlineData("/quartz/api/config")]
    public async Task GetApiEndpoint_DefaultConfiguration_ReturnsValidJson(string path)
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        response.AssertJsonContentType();

        using var json = await response.ReadJsonAsync();
        Assert.NotEqual(System.Text.Json.JsonValueKind.Undefined, json.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetScheduler_DefaultConfiguration_ReturnsSchedulerInfo()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/api/scheduler");
        using var json = await response.ReadJsonAsync();

        Assert.True(json.RootElement.TryGetProperty("name", out _));
        Assert.True(json.RootElement.TryGetProperty("threadPoolSize", out _));
    }

    [Fact]
    public async Task GetJobs_DefaultConfiguration_ReturnsJobList()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/api/jobs");
        using var json = await response.ReadJsonAsync();

        Assert.True(json.RootElement.TryGetProperty("data", out var jobs));
        Assert.True(json.RootElement.TryGetProperty("total", out var total));
        Assert.True(total.GetInt32() >= jobs.GetArrayLength());
    }

    [Fact]
    public async Task GetTriggers_DefaultConfiguration_ReturnsTriggerList()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/api/triggers");
        using var json = await response.ReadJsonAsync();

        Assert.True(json.RootElement.TryGetProperty("data", out var triggers));
        Assert.True(triggers.GetArrayLength() >= 4);
    }

    [Fact]
    public async Task GetHistory_DefaultConfiguration_ReturnsPagedHistory()
    {
        await _factory.WaitForHistoryAsync(2);

        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/api/history");
        using var json = await response.ReadJsonAsync();

        Assert.True(json.RootElement.TryGetProperty("data", out var history));
        Assert.Equal(System.Text.Json.JsonValueKind.Array, history.ValueKind);
    }

    [Fact]
    public async Task GetStats_DefaultConfiguration_ReturnsStatsObject()
    {
        await _factory.WaitForHistoryAsync(2);

        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/api/stats");
        using var json = await response.ReadJsonAsync();

        Assert.True(json.RootElement.TryGetProperty("percentiles", out _));
        Assert.True(json.RootElement.TryGetProperty("executionBuckets", out _));
    }

    [Fact]
    public async Task GetHealth_DefaultConfiguration_ReturnsHealthObject()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/api/health");
        using var json = await response.ReadJsonAsync();

        Assert.True(json.RootElement.TryGetProperty("status", out _));
    }

    [Fact]
    public async Task GetTimeline_DefaultConfiguration_ReturnsArray()
    {
        await _factory.WaitForHistoryAsync(2);

        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/api/timeline");
        using var json = await response.ReadJsonAsync();

        Assert.Equal(System.Text.Json.JsonValueKind.Array, json.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetCalendars_DefaultConfiguration_ReturnsArray()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/api/calendars");
        using var json = await response.ReadJsonAsync();

        Assert.Equal(System.Text.Json.JsonValueKind.Array, json.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetConfig_DefaultConfiguration_ReturnsDashboardSettings()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/api/config");
        using var json = await response.ReadJsonAsync();

        Assert.False(json.RootElement.GetProperty("readOnly").GetBoolean());
        Assert.Equal("/quartz", json.RootElement.GetProperty("basePath").GetString());
    }
}
