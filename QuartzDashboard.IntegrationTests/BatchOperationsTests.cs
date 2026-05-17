using System.Net;
using System.Net.Http.Json;
using QuartzDashboard.IntegrationTests.Fixtures;
using Xunit;

namespace QuartzDashboard.IntegrationTests;

[Collection(QuartzDashboardIntegrationCollection.Name)]
public sealed class BatchOperationsTests
{
    [Fact(Skip = "TODO: implement")]
    public async Task BatchDeleteJobs_WithValidKeys_DeletesAllJobs()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "TODO: implement")]
    public async Task BatchDeleteJobs_WithEmptyArray_ReturnsBadRequest()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "TODO: implement")]
    public async Task BatchPauseJobs_WithValidKeys_PausesAll()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "TODO: implement")]
    public async Task BatchTriggerJobs_WithDataMap_PropagatesPayload()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "TODO: implement")]
    public async Task BatchOperations_ReadOnlyMode_ReturnsForbidden()
    {
        await using var factory = new TestWebAppFactory(options => options.ReadOnly = true);
        using var client = factory.CreateAnonymousClient();

        var endpoints = new[]
        {
            "/quartz/api/jobs/batch/delete",
            "/quartz/api/jobs/batch/pause",
            "/quartz/api/jobs/batch/resume",
            "/quartz/api/jobs/batch/trigger",
        };

        foreach (var path in endpoints)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(Array.Empty<object>())
            };
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
