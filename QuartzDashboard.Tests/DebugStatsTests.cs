using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Xunit;

namespace QuartzDashboard.Tests;

[Collection("QuartzDashboard")]
public class DebugStatsTests : IClassFixture<QuartzTestFixture>
{
    private readonly HttpClient _client;

    public DebugStatsTests(QuartzTestFixture fixture)
    {
        _client = fixture.Client;
    }

    /// <summary>
    /// Debug test to inspect the actual stats endpoint response.
    /// </summary>
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
        Assert.True(root.TryGetProperty("totalExecutions", out _), "Missing 'totalExecutions'");
        Assert.True(root.TryGetProperty("uptimeMinutes", out _), "Missing 'uptimeMinutes'");
        Assert.True(root.TryGetProperty("schedulerVersion", out _), "Missing 'schedulerVersion'");
        Assert.True(root.TryGetProperty("threadPoolSize", out _), "Missing 'threadPoolSize'");
        Assert.True(root.TryGetProperty("executionBuckets", out _), "Missing 'executionBuckets'");
    }
}
