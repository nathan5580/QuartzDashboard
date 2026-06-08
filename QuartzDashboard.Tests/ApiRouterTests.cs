using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Quartz.Logging;
using Xunit;

namespace QuartzDashboard.Tests;

public sealed class ApiRouterTests
{
    [Fact]
    public async Task GetRootApi_WithoutSegment_Returns404()
    {
        await using var fixture = CreateFixture();
        var client = fixture.Client;

        var response = await client.GetAsync("/quartz/api");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Unknown endpoint", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetRequest_OnPostOnlyRoute_Returns404()
    {
        await using var fixture = CreateFixture();
        var client = fixture.Client;

        var response = await client.GetAsync("/quartz/api/scheduler/standby");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostRequest_OnGetOnlyRoute_Returns404()
    {
        await using var fixture = CreateFixture();
        var client = fixture.Client;

        var response = await client.PostAsync("/quartz/api/scheduler", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AllGetRoutes_Return200()
    {
        await using var fixture = CreateFixture();
        var client = fixture.Client;

        var getRoutes = new[]
        {
            "/quartz/api/health",
            "/quartz/api/config",
            "/quartz/api/schedulers",
            "/quartz/api/scheduler",
            "/quartz/api/jobs",
            "/quartz/api/triggers",
            "/quartz/api/executing",
            "/quartz/api/history",
            "/quartz/api/stats",
            "/quartz/api/stats/history",
            "/quartz/api/timeline",
            "/quartz/api/heatmap",
            "/quartz/api/calendars",
            "/quartz/api/export",
        };

        foreach (var route in getRoutes)
        {
            var response = await client.GetAsync(route);
            Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent,
                $"Route {route} returned {response.StatusCode}");
        }
    }

    [Fact]
    public async Task Route_WithV1Prefix_Works()
    {
        await using var fixture = CreateFixture();
        var client = fixture.Client;

        using var response = await client.GetAsync("/quartz/api/v1/scheduler");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Route_DeeplyNestedSegments_WithV1Prefix_Works()
    {
        await using var fixture = CreateFixture();
        var client = fixture.Client;

        using var response = await client.GetAsync("/quartz/api/v1/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(doc.RootElement.TryGetProperty("basePath", out _));
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsExpectedShape()
    {
        await using var fixture = CreateFixture();
        var client = fixture.Client;

        using var response = await client.GetAsync("/quartz/api/health");
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(doc.RootElement.TryGetProperty("status", out _));
        Assert.True(doc.RootElement.TryGetProperty("scheduler", out _));
        Assert.True(doc.RootElement.TryGetProperty("stats", out _));
    }

    [Fact]
    public async Task CronDescribe_Post_ReturnsValidResponse()
    {
        await using var fixture = CreateFixture();
        var client = fixture.Client;

        var payload = JsonContent.Create(new { expression = "0 0/5 * * * ?" });
        using var response = await client.PostAsync("/quartz/api/cron/describe", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(doc.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal(5, doc.RootElement.GetProperty("nextFireTimes").GetArrayLength());
    }

    [Fact]
    public async Task CronDescribe_WithInvalidExpression_ReturnsBadRequest()
    {
        await using var fixture = CreateFixture();
        var client = fixture.Client;

        var payload = JsonContent.Create(new { expression = "not a cron expression" });
        using var response = await client.PostAsync("/quartz/api/cron/describe", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.False(doc.RootElement.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task UnknownEndpoint_IncludesPathInError()
    {
        await using var fixture = CreateFixture();
        var client = fixture.Client;

        using var response = await client.GetAsync("/quartz/api/deep/nested/nonexistent/route");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Unknown endpoint", doc.RootElement.GetProperty("error").GetString());
        Assert.True(doc.RootElement.GetProperty("path").GetString()!.Contains("deep"));
    }

    [Fact]
    public async Task SchedulersEndpoint_ReturnsSchedulerList()
    {
        await using var fixture = CreateFixture();
        var client = fixture.Client;

        using var response = await client.GetAsync("/quartz/api/schedulers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() > 0);
    }

    [Fact]
    public async Task JobGroupPause_WithReadOnly_ReturnsForbidden()
    {
        await using var fixture = CreateFixture(o => o.ReadOnly = true);
        var client = fixture.Client;

        using var response = await client.PostAsync("/quartz/api/jobs/group/DEFAULT/pause", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TriggerGroupPause_WithReadOnly_ReturnsForbidden()
    {
        await using var fixture = CreateFixture(o => o.ReadOnly = true);
        var client = fixture.Client;

        using var response = await client.PostAsync("/quartz/api/triggers/group/DEFAULT/pause", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Import_WithReadOnly_ReturnsForbidden()
    {
        await using var fixture = CreateFixture(o => o.ReadOnly = true);
        var client = fixture.Client;

        using var response = await client.PostAsync("/quartz/api/import",
            JsonContent.Create(new { jobs = new[] { new { name = "test", group = "DEFAULT", jobType = "" } } }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static TestFixture CreateFixture(Action<QuartzDashboardOptions>? configure = null)
    {
        return new TestFixture(configure);
    }

    private static async Task CreateDurableJob(HttpClient client, string name, string group = "DEFAULT")
    {
        var resp = await client.PostAsync("/quartz/api/jobs",
            JsonContent.Create(new { name, group, description = "", jobType = "", isDurable = true }));
        resp.EnsureSuccessStatusCode();
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private IHost? _host;

        public HttpClient Client { get; }

        public TestFixture(Action<QuartzDashboardOptions>? configure)
        {
            ResetQuartzLogProvider();

            var id = Guid.NewGuid().ToString("N");
            _host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(services =>
                    {
                        services.AddQuartz(q =>
                        {
                            q.SchedulerId = id;
                            q.SchedulerName = id;
                        });
                        services.AddQuartzDashboard(options =>
                        {
                            options.UseSignalR = false;
                            options.RequireAuthentication = false;
                            options.RequireCsrfHeader = false;
                            configure?.Invoke(options);
                        });
                    });
                    web.Configure(app => app.UseQuartzDashboard());
                })
                .Start();

            var schedFactory = _host.Services.GetRequiredService<ISchedulerFactory>();
            var scheduler = schedFactory.GetScheduler().GetAwaiter().GetResult();
            if (!scheduler.IsStarted)
                scheduler.Start().GetAwaiter().GetResult();

            Client = _host.GetTestClient();
            Client.BaseAddress = new Uri("http://localhost");
        }

        public async ValueTask DisposeAsync()
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
                _host = null;
            }
            ResetQuartzLogProvider();
        }

        private static void ResetQuartzLogProvider()
        {
            var field = typeof(LogProvider).GetField("s_currentLogProvider",
                BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}
