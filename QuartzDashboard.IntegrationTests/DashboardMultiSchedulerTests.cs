using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using QuartzDashboard.IntegrationTests.Fixtures;
using Xunit;

namespace QuartzDashboard.IntegrationTests;

[Collection(QuartzDashboardIntegrationCollection.Name)]
public sealed class DashboardMultiSchedulerTests : IAsyncLifetime
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
    public async Task GetSchedulers_ReturnsSchedulerList()
    {
        using var client = _factory.CreateAnonymousClient();

        using var response = await client.GetAsync("/quartz/api/schedulers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await response.ReadJsonAsync();
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.True(json.RootElement.GetArrayLength() > 0);

        var first = json.RootElement[0];
        Assert.True(first.TryGetProperty("name", out var name));
        Assert.True(name.GetString()!.Length > 0);
        Assert.True(first.TryGetProperty("instanceId", out _));
        Assert.True(first.TryGetProperty("isStarted", out _));
    }

    [Fact]
    public async Task GetJobs_WithSchedulerQueryParam_UsesSpecifiedScheduler()
    {
        using var client = _factory.CreateAnonymousClient();

        var schedFactory = _factory.Server.Services
            .GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedFactory.GetScheduler();
        var meta = await scheduler.GetMetaData();
        var schedulerName = meta.SchedulerName;

        using var response = await client.GetAsync(
            $"/quartz/api/jobs?scheduler={Uri.EscapeDataString(schedulerName)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await response.ReadJsonAsync();
        Assert.True(json.RootElement.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task GetJobs_WithInvalidSchedulerName_ReturnsBadRequest()
    {
        using var client = _factory.CreateAnonymousClient();

        using var response = await client.GetAsync("/quartz/api/jobs?scheduler=%3Cscript%3Ealert(1)%3C/script%3E");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = await response.ReadJsonAsync();
        Assert.Equal("Invalid scheduler name", json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetJobs_WithOverlyLongSchedulerName_ReturnsBadRequest()
    {
        using var client = _factory.CreateAnonymousClient();

        var longName = new string('a', 101);
        using var response = await client.GetAsync(
            $"/quartz/api/jobs?scheduler={Uri.EscapeDataString(longName)}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
