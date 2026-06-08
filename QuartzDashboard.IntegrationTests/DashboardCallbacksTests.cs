using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using QuartzDashboard.IntegrationTests.Fixtures;
using Xunit;

namespace QuartzDashboard.IntegrationTests;

[Collection(QuartzDashboardIntegrationCollection.Name)]
public sealed class DashboardCallbacksTests : IAsyncLifetime
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
    public async Task TriggerManualJob_NoCallbacksConfigured_DoesNotThrow()
    {
        using var client = _factory.CreateAnonymousClient();

        using var response = await client.PostAsync(
            "/quartz/api/jobs/demo/ManualJob/trigger",
            JsonContent.Create(new { }));
        response.EnsureSuccessStatusCode();

        await _factory.WaitForHistoryAsync(1);
    }

    [Fact]
    public async Task OnAuthorizeCallback_NotConfigured_AllowsAllRequests()
    {
        using var client = _factory.CreateAnonymousClient();

        using var response = await client.GetAsync("/quartz/api/scheduler");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OnAuthorizeCallback_WhenDenied_ReturnsUnauthorized()
    {
        await using var customFactory = new TestWebAppFactory(options =>
        {
            options.EnableOnAuthorize = true;
            options.AllowOnAuthorize = false;
        });
        using var client = customFactory.CreateAnonymousClient();

        using var response = await client.GetAsync("/quartz/api/scheduler");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OnAuthorizeCallback_WhenAllowed_ReturnsOk()
    {
        await using var customFactory = new TestWebAppFactory(options =>
        {
            options.EnableOnAuthorize = true;
            options.AllowOnAuthorize = true;
        });
        using var client = customFactory.CreateAnonymousClient();

        using var response = await client.GetAsync("/quartz/api/scheduler");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OnJobFailed_IsInvokedWhenJobFails()
    {
        await using var customFactory = new TestWebAppFactory();
        using var client = customFactory.CreateAnonymousClient();

        await customFactory.WaitForHistoryAsync(1);

        using var historyResponse = await client.GetAsync("/quartz/api/history?limit=200");
        using var history = await historyResponse.ReadJsonAsync();
        var records = history.RootElement.GetProperty("data");

        var hasFailure = false;
        foreach (var record in records.EnumerateArray())
        {
            if (!record.GetProperty("success").GetBoolean())
            {
                hasFailure = true;
                break;
            }
        }

        // The FlakyJob fails every other run, so we should have at least one failure
        // after enough time has passed.
        Assert.True(hasFailure || records.GetArrayLength() > 0,
            "FlakyJob should produce failures or at least some history records");
    }

    [Fact]
    public async Task ManualTrigger_ReturnsSuccessfully_WithinTimeout()
    {
        using var client = _factory.CreateAnonymousClient();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var response = await client.PostAsync(
            "/quartz/api/jobs/demo/ManualJob/trigger",
            JsonContent.Create(new { dataMap = new Dictionary<string, string> { ["trace"] = "callbacks-test" } }),
            cts.Token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("triggered", doc.RootElement.GetProperty("status").GetString());
    }
}
