using System.Net;
using System.Net.Http.Json;
using QuartzDashboard.IntegrationTests.Fixtures;
using Xunit;

namespace QuartzDashboard.IntegrationTests;

[Collection(QuartzDashboardIntegrationCollection.Name)]
public sealed class DashboardReadOnlyTests
{
    [Theory]
    [InlineData("POST", "/quartz/api/scheduler/start")]
    [InlineData("POST", "/quartz/api/scheduler/standby")]
    [InlineData("POST", "/quartz/api/triggers/demo/FastJob-trigger/pause")]
    [InlineData("POST", "/quartz/api/triggers/demo/FastJob-trigger/resume")]
    [InlineData("POST", "/quartz/api/jobs/demo/ManualJob/trigger")]
    [InlineData("DELETE", "/quartz/api/jobs/demo/ManualJob")]
    [InlineData("DELETE", "/quartz/api/triggers/demo/FastJob-trigger")]
    public async Task MutationEndpoint_ReadOnlyMode_ReturnsForbidden(string method, string path)
    {
        await using var customFactory = new TestWebAppFactory(options => options.ReadOnly = true);
        using var client = customFactory.CreateAnonymousClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) ? JsonContent.Create(new { }) : null
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetEndpoints_ReadOnlyMode_StillWork()
    {
        await using var customFactory = new TestWebAppFactory(options => options.ReadOnly = true);
        using var client = customFactory.CreateAnonymousClient();

        using var jobsResponse = await client.GetAsync("/quartz/api/jobs");
        using var configResponse = await client.GetAsync("/quartz/api/config");
        using var config = await configResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, jobsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, configResponse.StatusCode);
        Assert.True(config.RootElement.GetProperty("readOnly").GetBoolean());
    }
}
