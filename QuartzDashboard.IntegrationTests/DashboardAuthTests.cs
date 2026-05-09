using System.Net;
using QuartzDashboard.IntegrationTests.Fixtures;
using Xunit;

namespace QuartzDashboard.IntegrationTests;

[Collection(QuartzDashboardIntegrationCollection.Name)]
public sealed class DashboardAuthTests : IAsyncLifetime
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
    public async Task GetScheduler_WithoutAuthRequirement_IsAccessibleAnonymously()
    {
        using var client = _factory.CreateAnonymousClient();
        using var response = await client.GetAsync("/quartz/api/scheduler");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetScheduler_RequireAuthenticationWithoutUser_ReturnsUnauthorized()
    {
        await using var customFactory = new TestWebAppFactory(options => options.RequireAuthentication = true);
        using var client = customFactory.CreateAnonymousClient();

        using var response = await client.GetAsync("/quartz/api/scheduler");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetScheduler_AllowedRolesWithWrongRole_ReturnsForbidden()
    {
        await using var customFactory = new TestWebAppFactory(options =>
        {
            options.RequireAuthentication = true;
            options.AllowedRoles = ["Admin"];
        });
        using var client = customFactory.CreateAuthenticatedClient(roles: ["User"]);

        using var response = await client.GetAsync("/quartz/api/scheduler");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetScheduler_AllowedRolesWithMatchingRole_ReturnsOk()
    {
        await using var customFactory = new TestWebAppFactory(options =>
        {
            options.RequireAuthentication = true;
            options.AllowedRoles = ["Admin"];
        });
        using var client = customFactory.CreateAuthenticatedClient(roles: ["Admin"]);

        using var response = await client.GetAsync("/quartz/api/scheduler");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetScheduler_RequiredPolicyWithoutClaim_ReturnsForbidden()
    {
        await using var customFactory = new TestWebAppFactory(options =>
        {
            options.RequireAuthentication = true;
            options.RequiredPolicy = "DashboardPolicy";
        });
        using var client = customFactory.CreateAuthenticatedClient();

        using var response = await client.GetAsync("/quartz/api/scheduler");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetScheduler_RequiredPolicyWithClaim_ReturnsOk()
    {
        await using var customFactory = new TestWebAppFactory(options =>
        {
            options.RequireAuthentication = true;
            options.RequiredPolicy = "DashboardPolicy";
        });
        using var client = customFactory.CreateAuthenticatedClient(permissions: ["dashboard"]);

        using var response = await client.GetAsync("/quartz/api/scheduler");
        using var configResponse = await client.GetAsync("/quartz/api/config");
        using var config = await configResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(config.RootElement.GetProperty("hasFullAccess").GetBoolean());
    }

    [Fact]
    public async Task GetScheduler_OnAuthorizeDenied_InvokesCallbackAndReturnsUnauthorized()
    {
        await using var customFactory = new TestWebAppFactory(options =>
        {
            options.EnableOnAuthorize = true;
            options.AllowOnAuthorize = false;
        });
        using var client = customFactory.CreateAnonymousClient();

        using var response = await client.GetAsync("/quartz/api/scheduler");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(customFactory.GetAuthorizeCount() > 0);
    }

    [Fact]
    public async Task GetWeather_RequireAuthenticationOnDashboard_DoesNotLeakToHostRoutes()
    {
        await using var customFactory = new TestWebAppFactory(options => options.RequireAuthentication = true);
        using var client = customFactory.CreateAnonymousClient();

        using var response = await client.GetAsync("/api/weather");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
