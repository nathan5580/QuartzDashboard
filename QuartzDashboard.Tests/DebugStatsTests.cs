using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Xunit;

namespace QuartzDashboard.Tests;

/// <summary>
/// Debug test to inspect the actual stats endpoint response.
/// </summary>
public class DebugStatsTests : IClassFixture<QuartzTestFixture>
{
    private readonly HttpClient _client;

    public DebugStatsTests(QuartzTestFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task Debug_Stats_Endpoint()
    {
        var response = await _client.GetAsync("/quartz/api/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Debug.WriteLine("=== STATS RESPONSE ===");
        Debug.WriteLine(json);

        // Parse and check all properties
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        foreach (var prop in root.EnumerateObject())
        {
            Debug.WriteLine($"  Property: '{prop.Name}' (type: {prop.Value.ValueKind})");
        }

        // Now test assertions
        Assert.True(root.TryGetProperty("TotalExecutions", out _), "Missing 'TotalExecutions'");
        Assert.True(root.TryGetProperty("UptimeMinutes", out _), "Missing 'UptimeMinutes'");
        Assert.True(root.TryGetProperty("SchedulerVersion", out _), "Missing 'SchedulerVersion'");
        Assert.True(root.TryGetProperty("ThreadPoolSize", out _), "Missing 'ThreadPoolSize'");
        Assert.True(root.TryGetProperty("ExecutionBuckets", out _), "Missing 'ExecutionBuckets'");
    }
}
