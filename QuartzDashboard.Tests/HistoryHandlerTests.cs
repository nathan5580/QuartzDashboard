using System.Net;
using System.Text.Json;
using Xunit;

namespace QuartzDashboard.Tests;

public class HistoryHandlerTests : IClassFixture<QuartzTestFixture>
{
    private readonly QuartzTestFixture _fixture;
    private readonly HttpClient _client;

    public HistoryHandlerTests(QuartzTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    // ── history ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFireHistory_ReturnsOkWithPaginationShape()
    {
        var response = await _client.GetAsync("/quartz/api/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("data", out _));
        Assert.True(doc.RootElement.TryGetProperty("total", out _));
        Assert.True(doc.RootElement.TryGetProperty("offset", out _));
        Assert.True(doc.RootElement.TryGetProperty("limit", out _));
    }

    [Fact]
    public async Task GetFireHistory_OffsetAndLimit_AreRespected()
    {
        var response = await _client.GetAsync("/quartz/api/history?offset=0&limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("offset").GetInt32());
        Assert.Equal(10, doc.RootElement.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task GetFireHistory_LimitCappedAt200()
    {
        var response = await _client.GetAsync("/quartz/api/history?limit=999");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(200, doc.RootElement.GetProperty("limit").GetInt32());
    }

    // ── timeline ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTimeline_ReturnsOkWithArray()
    {
        var response = await _client.GetAsync("/quartz/api/timeline");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    // ── stats ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_ReturnsOk()
    {
        var response = await _client.GetAsync("/quartz/api/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_HasAllExpectedFields()
    {
        var response = await _client.GetAsync("/quartz/api/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verify camelCase serialization for all top-level fields
        Assert.True(root.TryGetProperty("totalExecutions", out _));
        Assert.True(root.TryGetProperty("uptimeMinutes", out _));
        Assert.True(root.TryGetProperty("schedulerVersion", out _));
        Assert.True(root.TryGetProperty("threadPoolSize", out _));
        Assert.True(root.TryGetProperty("executionBuckets", out _));
        Assert.True(root.TryGetProperty("executionRate", out _));
        Assert.True(root.TryGetProperty("averageDurationMs", out _));
    }

    [Fact]
    public async Task GetStats_TotalExecutions_IsNonNegative()
    {
        var response = await _client.GetAsync("/quartz/api/stats");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var total = doc.RootElement.GetProperty("totalExecutions").GetInt64();
        Assert.True(total >= 0);
    }

    [Fact]
    public async Task GetStats_ExecutionBuckets_IsArray()
    {
        var response = await _client.GetAsync("/quartz/api/stats");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var buckets = doc.RootElement.GetProperty("executionBuckets");
        Assert.Equal(JsonValueKind.Array, buckets.ValueKind);
    }

    [Fact]
    public async Task GetStats_UptimeMinutes_IsNonNegative()
    {
        var response = await _client.GetAsync("/quartz/api/stats");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var uptime = doc.RootElement.GetProperty("uptimeMinutes").GetDouble();
        Assert.True(uptime >= 0);
    }
}
