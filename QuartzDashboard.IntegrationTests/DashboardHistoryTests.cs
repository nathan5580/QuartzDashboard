using QuartzDashboard.IntegrationTests.Fixtures;
using Xunit;

namespace QuartzDashboard.IntegrationTests;

[Collection(QuartzDashboardIntegrationCollection.Name)]
public sealed class DashboardHistoryTests(TestWebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateAnonymousClient();

    [Fact]
    public async Task GetHistory_AfterJobsExecute_ReturnsRecords()
    {
        await factory.WaitForHistoryAsync(4);

        using var response = await _client.GetAsync("/quartz/api/history?limit=20");
        using var json = await response.ReadJsonAsync();

        Assert.True(json.RootElement.GetProperty("data").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetHistory_RecordContainsExpectedFields()
    {
        await factory.WaitForHistoryAsync(4);

        using var response = await _client.GetAsync("/quartz/api/history?limit=1");
        using var json = await response.ReadJsonAsync();
        var record = json.RootElement.GetProperty("data")[0];

        Assert.True(record.TryGetProperty("jobKey", out _));
        Assert.True(record.TryGetProperty("triggerKey", out _));
        Assert.True(record.TryGetProperty("fireTime", out _));
        Assert.True(record.TryGetProperty("duration", out _));
        Assert.True(record.TryGetProperty("success", out _));
    }

    [Fact]
    public async Task GetStats_AfterJobsExecute_ReturnsPercentiles()
    {
        await factory.WaitForHistoryAsync(4);

        using var response = await _client.GetAsync("/quartz/api/stats");
        using var json = await response.ReadJsonAsync();
        var percentiles = json.RootElement.GetProperty("percentiles");

        Assert.True(percentiles.GetProperty("p50").GetDouble() >= 0);
        Assert.True(percentiles.GetProperty("p95").GetDouble() >= 0);
        Assert.True(percentiles.GetProperty("p99").GetDouble() >= 0);
    }

    [Fact]
    public async Task GetTimeline_AfterJobsExecute_HasEntries()
    {
        await factory.WaitForHistoryAsync(4);

        using var response = await _client.GetAsync("/quartz/api/timeline");
        using var json = await response.ReadJsonAsync();

        Assert.True(json.RootElement.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetGraphHistory_AfterJobsExecute_HasEntries()
    {
        await factory.WaitForHistoryAsync(4);

        using var response = await _client.GetAsync("/quartz/api/stats/history");
        using var json = await response.ReadJsonAsync();

        Assert.True(json.RootElement.GetArrayLength() > 0);
    }
}
