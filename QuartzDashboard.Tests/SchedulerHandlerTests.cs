using System.Net;
using System.Text.Json;
using Xunit;

namespace QuartzDashboard.Tests;

/// <summary>
/// Tests that scheduler API endpoints return correct responses via TestServer.
/// Uses shared CollectionFixture to avoid Quartz LogProvider disposal conflicts.
/// </summary>
[Collection("QuartzDashboard")]
public class SchedulerHandlerTests : IClassFixture<QuartzTestFixture>
{
    private readonly QuartzTestFixture _fixture;
    private readonly HttpClient _client;

    public SchedulerHandlerTests(QuartzTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task Health_Endpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/quartz/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("status", out var status));
        Assert.True(status.GetString() == "healthy" || status.GetString() == "degraded");

        Assert.True(root.TryGetProperty("scheduler", out var scheduler));
        Assert.Equal("TestScheduler", scheduler.GetProperty("name").GetString());
        Assert.True(scheduler.GetProperty("isStarted").GetBoolean());
        Assert.False(scheduler.GetProperty("isStandby").GetBoolean());

        Assert.True(root.TryGetProperty("stats", out var stats));
        Assert.True(stats.GetProperty("totalExecutions").GetInt64() >= 0);
        Assert.True(stats.GetProperty("historyCount").GetInt32() >= 0);
        Assert.True(stats.GetProperty("threadPoolSize").GetInt32() > 0);
    }

    [Fact]
    public async Task Health_Endpoint_WithV1Prefix_ReturnsOk()
    {
        var response = await _client.GetAsync("/quartz/api/v1/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("status", out _));
    }

    [Fact]
    public async Task Scheduler_Endpoint_ReturnsSchedulerInfo()
    {
        var response = await _client.GetAsync("/quartz/api/scheduler");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("TestScheduler", root.GetProperty("name").GetString());
        Assert.True(root.GetProperty("isStarted").GetBoolean());
    }

    [Fact]
    public async Task Scheduler_Endpoint_WithV1Prefix_ReturnsSchedulerInfo()
    {
        var response = await _client.GetAsync("/quartz/api/v1/scheduler");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("TestScheduler", root.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Config_Endpoint_ReturnsConfig()
    {
        var response = await _client.GetAsync("/quartz/api/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("readOnly").GetBoolean());
        Assert.False(root.GetProperty("useSignalR").GetBoolean());
        Assert.Equal("/quartz", root.GetProperty("basePath").GetString());
        Assert.False(root.GetProperty("hasWebhookConfigured").GetBoolean());
        Assert.False(root.TryGetProperty("webhookUrl", out _));
    }

    [Fact]
    public async Task Executing_Endpoint_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/quartz/api/executing");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Shape is { data: [], total: 0 } since v4.2 (PagedResponse).
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.Equal(0, data.GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Stats_Endpoint_ReturnsStats()
    {
        var response = await _client.GetAsync("/quartz/api/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("totalExecutions", out _));
        Assert.True(root.TryGetProperty("uptimeMinutes", out _));
        Assert.True(root.TryGetProperty("schedulerVersion", out _));
        Assert.True(root.TryGetProperty("threadPoolSize", out _));
        Assert.True(root.TryGetProperty("executionBuckets", out _));
    }

    [Fact]
    public async Task UnknownEndpoint_ReturnsJsonNotFoundError()
    {
        var response = await _client.GetAsync("/quartz/api/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Unknown endpoint", doc.RootElement.GetProperty("error").GetString());
        Assert.True(doc.RootElement.TryGetProperty("path", out _));
    }

    [Fact]
    public async Task Scheduler_StandbyAndStart_WorkCorrectly()
    {
        // Put scheduler in standby
        var standbyResponse = await _client.PostAsync("/quartz/api/scheduler/standby", null);
        Assert.Equal(HttpStatusCode.OK, standbyResponse.StatusCode);

        var json = await standbyResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.GetProperty("status").GetString();
        Assert.Contains(status, new[] { "standby", "already_standby" });

        // Start scheduler again
        var startResponse = await _client.PostAsync("/quartz/api/scheduler/start", null);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);

        json = await startResponse.Content.ReadAsStringAsync();
        using var doc2 = JsonDocument.Parse(json);
        var status2 = doc2.RootElement.GetProperty("status").GetString();
        Assert.Contains(status2, new[] { "started", "already_running" });
    }
}
