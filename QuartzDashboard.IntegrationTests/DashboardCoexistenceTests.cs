using System.Net;
using QuartzDashboard.IntegrationTests.Fixtures;
using Xunit;

namespace QuartzDashboard.IntegrationTests;

[Collection(QuartzDashboardIntegrationCollection.Name)]
public sealed class DashboardCoexistenceTests(TestWebAppFactory factory)
{
    [Fact]
    public async Task GetWeather_DefaultHostConfiguration_StillResponds()
    {
        using var client = factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/api/weather");
        using var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, json.RootElement.ValueKind);
    }

    [Fact]
    public async Task Requests_HostAndDashboard_BothFlowThroughHostMiddleware()
    {
        using var client = factory.CreateAnonymousClient();

        using var hostResponse = await client.GetAsync("/api/health");
        using var dashboardResponse = await client.GetAsync("/quartz/api/scheduler");

        Assert.Equal("executed", hostResponse.Headers.GetValues("X-Test-Middleware").Single());
        Assert.Equal("executed", dashboardResponse.Headers.GetValues("X-Test-Middleware").Single());
    }

    [Fact]
    public async Task Routes_DashboardAndHostEndpoints_DoNotConflict()
    {
        using var client = factory.CreateAnonymousClient();

        using var dashboardResponse = await client.GetAsync("/quartz/api/jobs");
        using var hostApiResponse = await client.GetAsync("/api/quartz-host/status");
        using var hostRootResponse = await client.GetAsync("/quartz-status");

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, hostApiResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, hostRootResponse.StatusCode);
    }

    [Fact]
    public async Task SecureHostRoute_UsesHostAuthenticationIndependently()
    {
        using var anonymousClient = factory.CreateAnonymousClient();
        using var anonymousResponse = await anonymousClient.GetAsync("/api/secure/ping");
        using var authenticatedClient = factory.CreateAuthenticatedClient();
        using var authenticatedResponse = await authenticatedClient.GetAsync("/api/secure/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);
    }

    [Fact]
    public async Task IndependentFactories_DifferentDashboardPaths_WorkWithoutRouteConflicts()
    {
        var defaultFactory = factory.WithScenario(_ => { });
        var customFactory = factory.WithScenario(options => options.Path = "/admin/scheduler");
        using var defaultClient = defaultFactory.CreateAnonymousClient();
        using var customClient = customFactory.CreateAnonymousClient();

        using var defaultResponse = await defaultClient.GetAsync("/quartz/");
        using var customResponse = await customClient.GetAsync("/admin/scheduler/");

        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, customResponse.StatusCode);
    }
}
